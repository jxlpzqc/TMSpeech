#!/usr/bin/env python3
#
# Copyright (c)  2025  Xiaomi Corporation

"""
Common utilities for audio recording and device management.
Shared by simulate-streaming.py and simulate-streaming-sense-voice-microphone.py
"""

import sys
import threading
import time
import queue
from pathlib import Path
import os
import tkinter as tk
import wave

try:
    import pyaudiowpatch as pyaudio
    import sherpa_onnx
    from scipy import signal
    import numpy as np
except ImportError:
    print("正在安装需要的python包:\n", file=sys.stderr)
    print("尝试执行：  pip install PyAudioWPatch sherpa_onnx==1.12.19 scipy\n", file=sys.stderr)
    ret = os.system(f"{sys.executable} -m pip install PyAudioWPatch sherpa_onnx==1.12.19 scipy")
    if ret == 0:
        import pyaudiowpatch as pyaudio
        import sherpa_onnx
        from scipy import signal
        import numpy as np
    else:
        print("安装python包失败!!", file=sys.stderr)
        sys.exit(-1)

DEBUG = False

# Global variables
killed = False
recording_process = None
sample_rate = 16000  # Please don't change it
audio_stream = None
p = None
samples_queue = None
stop_event = None

samples_time = 0.05 # 0.05s

def assert_file_exists(filename: str):
    """Assert that a file exists, with helpful error message."""
    assert Path(filename).is_file(), (
        f"{filename} does not exist!\n"
        "Please refer to "
        "https://k2-fsa.github.io/sherpa/onnx/pretrained_models/index.html to download it"
    )


def start_recording(device_indices, output_queue, stop_event, mix_mode="average", debug_save_audio=""):
    """
    支持多设备录音，使用设备原生采样率，然后重采样到目标采样率并混音
    使用基于时间戳的队列同步机制
    在独立进程中运行，通过output_queue发送音频数据，通过stop_event接收停止信号

    Args:
        device_indices: 设备索引列表
        output_queue: 输出队列
        stop_event: 停止事件
        mix_mode: 混音模式，"average"=平均混音，"add"=加法混音
        debug_save_audio: 调试模式，保存混音后的音频到指定的WAV文件路径
    """
    if not device_indices:
        print("没有选择任何设备！", file=sys.stderr)
        return

    p = pyaudio.PyAudio()

    # 调试模式：创建WAV文件用于保存混音结果
    debug_wav_file = None
    if debug_save_audio:
        try:
            debug_wav_file = wave.open(debug_save_audio, 'wb')
            debug_wav_file.setnchannels(1)  # 单声道
            debug_wav_file.setsampwidth(2)  # 16位
            debug_wav_file.setframerate(sample_rate)
            print(f"调试模式：混音音频将保存到 {debug_save_audio}", file=sys.stderr)
        except Exception as e:
            print(f"无法创建调试音频文件 {debug_save_audio}: {e}", file=sys.stderr)
            debug_wav_file = None

    # 为每个设备创建队列和流
    device_queues = {}  # 存储每个设备的数据队列
    device_streams = {}
    device_threads = {}
    device_info_map = {}

    def round_timestamp(ts):
        """将时间戳四舍五入到最近的samples_time"""
        return round(ts / samples_time) * samples_time

    def device_capture_thread(device_idx, native_rate, channels):
        """每个设备的采集线程 - 带时间戳放入队列"""
        samples_per_read = int(samples_time * native_rate)

        try:
            while not stop_event.is_set():
                # 读取音频数据
                data = device_streams[device_idx].read(samples_per_read, exception_on_overflow=False)

                # 获取当前时间戳并四舍五入到最近的samples_time
                timestamp = round_timestamp(time.time())

                # 转换为numpy数组
                samples = np.frombuffer(data, dtype=np.float32)

                # 如果是多声道，转换为单声道
                if channels > 1:
                    samples = samples.reshape(-1, channels)
                    samples = np.mean(samples, axis=1)

                samples = np.copy(samples)

                # 放入队列，带时间戳
                try:
                    device_queues[device_idx].put((timestamp, samples, native_rate), block=False)
                except queue.Full:
                    # 队列满了，丢弃最旧的数据
                    try:
                        device_queues[device_idx].get_nowait()
                        device_queues[device_idx].put((timestamp, samples, native_rate), block=False)
                    except:
                        pass

        except Exception as e:
            if not stop_event.is_set():
                print(f"设备 {device_idx} 采集出错: {e}", file=sys.stderr)

    # 为每个设备创建流和线程
    try:
        for device_idx in device_indices:
            device_info = p.get_device_info_by_index(device_idx)
            device_info_map[device_idx] = device_info

            native_rate = int(device_info['defaultSampleRate'])
            channels = device_info['maxInputChannels']
            if channels == 0:  # loopback设备
                channels = device_info['maxOutputChannels']

            samples_per_read = int(samples_time * native_rate)

            # 创建音频流
            stream = p.open(
                format=pyaudio.paFloat32,
                channels=channels,
                rate=native_rate,
                input=True,
                input_device_index=device_idx,
                frames_per_buffer=samples_per_read
            )

            device_streams[device_idx] = stream
            # 创建队列，最多保存10个数据包
            device_queues[device_idx] = queue.Queue(maxsize=10)

            # 启动采集线程
            thread = threading.Thread(
                target=device_capture_thread,
                args=(device_idx, native_rate, channels)
            )
            thread.start()
            device_threads[device_idx] = thread

            print(f"设备 {device_idx} ({device_info['name']}) 已启动，采样率: {native_rate} Hz", file=sys.stderr)

        # 混音线程：基于时间戳同步处理
        last_processed_timestamp = -1  # 已处理的最新时间戳
        device_pending_data = {}  # 每个设备已获取但未处理的数据槽位
        PROCESSING_DELAY = 3 * samples_time  # 处理延迟：3倍samples_time

        while not stop_event.is_set():
            current_time = time.time()

            # Phase 1: 从队列中获取数据到pending槽位
            for device_idx in device_indices:
                # 如果该设备还没有pending数据，尝试从队列获取
                if device_idx not in device_pending_data:
                    try:
                        packet = device_queues[device_idx].get_nowait()
                        device_pending_data[device_idx] = packet
                    except queue.Empty:
                        pass  # 该设备暂时没有新数据

            # Phase 2: 检查pending数据是否可以处理
            # 找到所有设备中最早的时间戳
            pending_timestamps = []
            for device_idx in device_indices:
                if device_idx in device_pending_data:
                    timestamp, _, _ = device_pending_data[device_idx]
                    pending_timestamps.append(timestamp)

            if not pending_timestamps:
                # 没有任何pending数据，短暂休眠后继续
                time.sleep(0.01)
                continue

            # 使用最早的时间戳作为目标处理时间戳
            target_timestamp = min(pending_timestamps)

            # 检查是否应该丢弃（时间戳过旧）
            if target_timestamp <= last_processed_timestamp:
                # 丢弃所有该时间戳的数据
                for device_idx in list(device_pending_data.keys()):
                    timestamp, _, _ = device_pending_data[device_idx]
                    if timestamp == target_timestamp:
                        del device_pending_data[device_idx]
                        msg = f"丢弃过旧数据包: 设备 {device_idx}, 时间戳 {timestamp:.3f}"
                        if DEBUG:
                            print(msg, file=sys.stderr)
                continue

            # 检查是否已经足够旧（达到处理延迟）
            age = current_time - target_timestamp
            if age < PROCESSING_DELAY:
                # 还不够旧，等待
                wait_time = PROCESSING_DELAY - age
                time.sleep(min(wait_time, 0.01))  # 最多等待10ms
                continue

            # Phase 3: 处理该时间戳的数据
            # 收集所有该时间戳的设备数据
            device_data_ready = {}
            for device_idx in device_indices:
                if device_idx in device_pending_data:
                    timestamp, samples, native_rate = device_pending_data[device_idx]
                    if timestamp == target_timestamp:
                        device_data_ready[device_idx] = (samples, native_rate)
                        # 从pending中移除
                        del device_pending_data[device_idx]

            # 检查是否所有设备都有该时间戳的数据
            if len(device_data_ready) < len(device_indices):
                msg = f"时间戳 {target_timestamp:.3f}: {len(device_data_ready)}/{len(device_indices)} 设备就绪"
                if DEBUG:
                    print(msg, file=sys.stderr)

            if not device_data_ready:
                continue  # 没有数据可处理

            # Phase 4: 重采样和混音
            resampled_samples = []
            for device_idx in device_indices:
                if device_idx not in device_data_ready:
                    continue  # 跳过没有数据的设备

                samples, native_rate = device_data_ready[device_idx]

                # 重采样到目标采样率
                if native_rate != sample_rate:
                    num_samples = int(len(samples) * sample_rate / native_rate)
                    resampled = signal.resample(samples, num_samples)
                else:
                    resampled = samples

                resampled_samples.append(resampled)

            # 混音：根据mix_mode选择混音算法
            if resampled_samples:
                if len(resampled_samples) == 1:
                    mixed = resampled_samples[0]
                else:
                    # 找到最短的长度，避免长度不匹配
                    max_length = max(len(s) for s in resampled_samples)
                    min_length = min(len(s) for s in resampled_samples)
                    if max_length != min_length:
                        print(f"长度不匹配：{min_length} - {max_length}", file=sys.stderr)
                    trimmed_samples = [s[:min_length] for s in resampled_samples]

                    # 根据混音模式选择算法
                    if mix_mode == "add":
                        mixed = np.sum(trimmed_samples, axis=0)
                    else:  # average
                        mixed = np.mean(trimmed_samples, axis=0)

                # 调试模式：保存混音结果到WAV文件
                if debug_wav_file:
                    try:
                        # 将float32转换为int16
                        audio_int16 = np.int16(mixed * 32767)
                        debug_wav_file.writeframes(audio_int16.tobytes())
                    except Exception as e:
                        print(f"写入调试音频文件出错: {e}", file=sys.stderr)

                output_queue.put(mixed)

                # 更新已处理的时间戳
                last_processed_timestamp = target_timestamp

    finally:
        # 清理资源
        for stream in device_streams.values():
            if stream:
                stream.stop_stream()
                stream.close()

        for thread in device_threads.values():
            if thread and thread.is_alive():
                thread.join(timeout=2.0)

        if p:
            p.terminate()

        # 关闭调试音频文件
        if debug_wav_file:
            try:
                debug_wav_file.close()
                print(f"调试音频已保存到 {debug_save_audio}", file=sys.stderr)
            except Exception as e:
                print(f"关闭调试音频文件出错: {e}", file=sys.stderr)


class MyPrinter:
    """Simple printer that avoids duplicate output."""
    def __init__(self):
        self.prev_result = ""

    def do_print(self, result):
        if result and self.prev_result != result:
            self.prev_result = result
            print(result, end='\n', flush=True)

    def on_endpoint(self):
        print("\n", end="", flush=True)


def select_input_device(devices, p_audio):
    """弹出tkinter窗口让用户选择输入设备或loopback设备（支持多选）"""
    # 过滤有输入通道的设备（普通输入设备）
    input_devices = [(i, d) for i, d in devices if d['maxInputChannels'] > 0 and not d.get('isLoopbackDevice', False)]

    # 获取loopback设备
    loopback_devices = []
    for loopback in p_audio.get_loopback_device_info_generator():
        loopback_devices.append((loopback['index'], loopback))

    if not input_devices and not loopback_devices:
        return None

    selected_devices = [[]]
    device_vars = {}  # 存储每个设备的checkbox变量

    def on_ok():
        # 收集所有被选中的设备
        selected = [idx for idx, var in device_vars.items() if var.get()]
        selected_devices[0] = selected
        root.destroy()

    root = tk.Tk()
    root.title("TMSpeech-CommandRecognizer: 选择输入设备（可多选）")
    root.geometry("900x500")

    # 创建主框架
    main_frame = tk.Frame(root)
    main_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

    # 左侧框架 - 输入设备
    left_frame = tk.Frame(main_frame, relief=tk.RIDGE, borderwidth=2)
    left_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=5)

    tk.Label(left_frame, text="输入设备", font=("Arial", 12, "bold")).pack(pady=5)

    # 创建滚动区域
    left_canvas = tk.Canvas(left_frame)
    left_scrollbar = tk.Scrollbar(left_frame, orient="vertical", command=left_canvas.yview)
    left_scrollable_frame = tk.Frame(left_canvas)

    left_scrollable_frame.bind(
        "<Configure>",
        lambda e: left_canvas.configure(scrollregion=left_canvas.bbox("all"))
    )

    left_canvas.create_window((0, 0), window=left_scrollable_frame, anchor="nw")
    left_canvas.configure(yscrollcommand=left_scrollbar.set)

    # 添加默认输入设备选项
    try:
        default_input = p_audio.get_default_input_device_info()
        default_idx = default_input['index']
        device_vars[default_idx] = tk.BooleanVar(value=True)  # 默认选中
        cb = tk.Checkbutton(
            left_scrollable_frame,
            text=f"[默认] {default_idx}: {default_input['name']}",
            variable=device_vars[default_idx],
            wraplength=380,
            justify=tk.LEFT,
            font=("Arial", 9, "bold")
        )
        cb.pack(anchor='w', padx=10, pady=3)

        # 添加分隔线
        tk.Frame(left_scrollable_frame, height=2, bg="gray").pack(fill=tk.X, padx=10, pady=5)
    except:
        if input_devices:
            # 如果没有默认设备，选中第一个输入设备
            device_vars[input_devices[0][0]] = tk.BooleanVar(value=True)

    # 添加其他输入设备
    for idx, device in input_devices:
        if idx not in device_vars:  # 避免重复添加默认设备
            device_vars[idx] = tk.BooleanVar(value=False)
        cb = tk.Checkbutton(
            left_scrollable_frame,
            text=f"{idx}: {device['name']}",
            variable=device_vars[idx],
            wraplength=380,
            justify=tk.LEFT
        )
        cb.pack(anchor='w', padx=10, pady=2)

    left_canvas.pack(side="left", fill="both", expand=True)
    left_scrollbar.pack(side="right", fill="y")

    # 右侧框架 - Loopback设备
    right_frame = tk.Frame(main_frame, relief=tk.RIDGE, borderwidth=2)
    right_frame.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True, padx=5)

    tk.Label(right_frame, text="录内音（可静音）", font=("Arial", 12, "bold")).pack(pady=5)

    # 创建滚动区域
    right_canvas = tk.Canvas(right_frame)
    right_scrollbar = tk.Scrollbar(right_frame, orient="vertical", command=right_canvas.yview)
    right_scrollable_frame = tk.Frame(right_canvas)

    right_scrollable_frame.bind(
        "<Configure>",
        lambda e: right_canvas.configure(scrollregion=right_canvas.bbox("all"))
    )

    right_canvas.create_window((0, 0), window=right_scrollable_frame, anchor="nw")
    right_canvas.configure(yscrollcommand=right_scrollbar.set)

    # 添加默认loopback设备选项
    try:
        wasapi_info = p_audio.get_host_api_info_by_type(pyaudio.paWASAPI)
        default_speakers = p_audio.get_device_info_by_index(wasapi_info["defaultOutputDevice"])

        # 查找对应的loopback设备
        default_loopback = None
        if not default_speakers.get("isLoopbackDevice", False):
            for loopback in p_audio.get_loopback_device_info_generator():
                if default_speakers["name"] in loopback["name"]:
                    default_loopback = loopback
                    break
        else:
            default_loopback = default_speakers

        if default_loopback:
            device_vars[default_loopback['index']] = tk.BooleanVar(value=False)
            cb = tk.Checkbutton(
                right_scrollable_frame,
                text=f"[默认] {default_loopback['index']}: {default_loopback['name']}",
                variable=device_vars[default_loopback['index']],
                wraplength=380,
                justify=tk.LEFT,
                font=("Arial", 9, "bold")
            )
            cb.pack(anchor='w', padx=10, pady=3)

            # 添加分隔线
            tk.Frame(right_scrollable_frame, height=2, bg="gray").pack(fill=tk.X, padx=10, pady=5)
    except:
        pass

    # 添加其他loopback设备
    for idx, device in loopback_devices:
        if idx not in device_vars:  # 避免重复添加默认设备
            device_vars[idx] = tk.BooleanVar(value=False)
        cb = tk.Checkbutton(
            right_scrollable_frame,
            text=f"{idx}: {device['name']}",
            variable=device_vars[idx],
            wraplength=380,
            justify=tk.LEFT
        )
        cb.pack(anchor='w', padx=10, pady=2)

    right_canvas.pack(side="left", fill="both", expand=True)
    right_scrollbar.pack(side="right", fill="y")

    # 确定按钮
    tk.Button(root, text="确定", command=on_ok, font=("Arial", 10)).pack(pady=10)

    root.mainloop()

    return selected_devices[0] if selected_devices[0] else []


def get_audio_devices(p_audio):
    """
    获取所有音频设备信息（仅MME主机API）

    Returns:
        list: 设备信息列表，每个元素为 (index, device_info) 元组
    """
    device_count = p_audio.get_device_count()

    if device_count == 0:
        return []

    # 设备太多，仅显示一部分
    host_api = 0
    for i in range(p_audio.get_host_api_count()):
        host_api_info = p_audio.get_host_api_info_by_index(i)
        if "MME" in host_api_info['name']:
            host_api = i

    # 获取所有设备信息
    devices = []
    for i in range(device_count):
        device_info = p_audio.get_device_info_by_index(i)
        if device_info['hostApi'] == host_api:
            devices.append((i, device_info))

    return devices


def cleanup_recording_process(stop_event, recording_process):
    """
    清理录音进程的辅助函数

    Args:
        stop_event: multiprocessing.Event 停止事件
        recording_process: multiprocessing.Process 录音进程
    """
    # 通知录音子进程停止
    if stop_event:
        stop_event.set()
    # 等待录音子进程结束
    if recording_process and recording_process.is_alive():
        recording_process.join(timeout=3.0)
        if recording_process.is_alive():
            recording_process.terminate()
            recording_process.join()
