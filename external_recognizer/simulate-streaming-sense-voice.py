#!/usr/bin/env python3
#
# Copyright (c)  2025  Xiaomi Corporation

"""
This file demonstrates how to use sherpa-onnx Python APIs
with VAD and non-streaming SenseVoice for real-time speech recognition
from a microphone.

Requirements:

wget https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09.tar.bz2
wget https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.onnx

Usage:

./python-api-examples/simulate-streaming-sense-voice-microphone.py  \
  --silero-vad-model=./silero_vad.onnx \
  --sense-voice=./sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17/model.onnx \
  --tokens=./sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17/tokens.txt
"""
import argparse
import sys
import multiprocessing
import time
from pathlib import Path
import os

script_path = os.path.realpath(__file__)
script_dir = os.path.dirname(script_path)
sys.path.insert(0, script_dir)

# Import common utilities
from common_audio_utils import (
    pyaudio, sherpa_onnx, np,
    sample_rate, assert_file_exists,
    start_recording, MyPrinter, select_input_device,
    get_audio_devices, cleanup_recording_process
)

vad_model_url = 'https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.onnx'
vad_model_path = os.path.join(script_dir, "silero_vad.onnx")
onnx_model_url = 'https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09.tar.bz2'
onnx_model_path = os.path.join(script_dir, "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09", "model.int8.onnx")
tokens_txt_path = os.path.join(script_dir, "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09", "tokens.txt")

# Global variables for this script
killed = False
recording_process = None
samples_queue = None
stop_event = None


def get_args():
    parser = argparse.ArgumentParser(
        formatter_class=argparse.ArgumentDefaultsHelpFormatter
    )
    
    parser.add_argument(
        "--device",
        type=int,
        default=-1,
        help="输入设备的编号",
    )

    parser.add_argument(
        "--silero-vad-model",
        type=str,
        help="silero_vad.onnx 文件路径",
        default=vad_model_path
    )

    parser.add_argument(
        "--tokens",
        type=str,
        help="模型的tokens.txt文件路径",
        default=tokens_txt_path
    )

    parser.add_argument(
        "--sense-voice",
        default=onnx_model_path,
        type=str,
        help="SenseVoice模型的model.onnx文件",
    )

    parser.add_argument(
        "--num-threads",
        type=int,
        default=2,
        help="用于推理的线程数，默认为2",
    )

    parser.add_argument(
        "--hr-lexicon",
        type=str,
        default="",
        help="If not empty, it is the lexicon.txt for homophone replacer",
    )

    parser.add_argument(
        "--hr-rule-fsts",
        type=str,
        default="",
        help="If not empty, it is the replace.fst for homophone replacer",
    )

    parser.add_argument(
        "--mix-mode",
        type=str,
        default="average",
        choices=["average", "add"],
        help="多设备混音模式：average=平均混音，add=加法混音（默认：average）",
    )

    parser.add_argument(
        "--debug-save-audio",
        type=str,
        default="",
        help="调试模式：保存混音后的音频到指定的WAV文件路径（例如：debug_mixed.wav）",
    )

    return parser.parse_args()


def try_download_model(vad_model_path, onnx_model_path):
    if not os.path.exists(vad_model_path):
        print(f"请下载文件到：{vad_model_path}", file=sys.stderr)
        print(f"下载链接：{vad_model_url}", file=sys.stderr)
    if not os.path.exists(onnx_model_path):
        print(f"请下载并解压文件夹到：{onnx_model_path}", file=sys.stderr)
        print(f"下载链接：{onnx_model_url}", file=sys.stderr)

def create_recognizer(args):
    assert_file_exists(args.sense_voice)
    recognizer = sherpa_onnx.OfflineRecognizer.from_sense_voice(
        model=args.sense_voice,
        tokens=args.tokens,
        num_threads=args.num_threads,
        use_itn=False,
        debug=False,
        hr_rule_fsts=args.hr_rule_fsts,
        hr_lexicon=args.hr_lexicon,
    )

    return recognizer


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
    p_temp = pyaudio.PyAudio()

    # 获取所有设备信息
    devices = get_audio_devices(p_temp)

    if not devices:
        print("没有任何输入设备！", file=sys.stderr)
        p_temp.terminate()
        sys.exit(0)

    args = get_args()

    print("可用设备:", file=sys.stderr)
    for idx, device in devices:
        print(f"  {idx}: {device['name']} (输入通道: {device['maxInputChannels']}, 输出通道: {device['maxOutputChannels']})", file=sys.stderr)

    # 如果命令行没有指定设备，弹出选择框
    selected_device_indices = []
    if args.device < 0:
        selected_device_indices = select_input_device(devices, p_temp)
        if not selected_device_indices:
            # 如果没有选择设备，使用默认输入设备
            default_info = p_temp.get_default_input_device_info()
            selected_device_indices = [default_info['index']]
    else:
        selected_device_indices = [args.device]

    # 关闭临时的PyAudio实例
    p_temp.terminate()

    # 如果你想要选择其他的输入设备，请解除下面这行的注释，并将 xxx 改为设备的序号
    # selected_device_indices = [xxx]
    # 注意要选设备结尾的in大于零的，比如 (2 in, 0 out) 表示两声道输入，没有输出声道，说明是录音设备。
    # 如果想要识别系统声音，尝试启用"立体声混音"，并设置识别设备为它。

    # 显示所有选中的设备
    device_names = []
    for idx in selected_device_indices:
        device_name = next((d['name'] for i, d in devices if i == idx), f"设备{idx}")
        device_names.append(f"{idx}: {device_name}")

    if len(selected_device_indices) == 1:
        print(f'使用输入设备: {device_names[0]}', file=sys.stderr)
    else:
        print(f'使用 {len(selected_device_indices)} 个输入设备:', file=sys.stderr)
        for name in device_names:
            print(f'  - {name}', file=sys.stderr)

    try_download_model(args.silero_vad_model, args.silero_vad_model)
    assert_file_exists(args.tokens)
    assert_file_exists(args.silero_vad_model)

    assert args.num_threads > 0, args.num_threads

    print("正在启动识别器，请稍后", file=sys.stderr)
    recognizer = create_recognizer(args)

    config = sherpa_onnx.VadModelConfig()
    config.silero_vad.model = args.silero_vad_model
    config.silero_vad.threshold = 0.5
    config.silero_vad.min_silence_duration = 0.1  # seconds
    config.silero_vad.min_speech_duration = 0.25  # seconds
    # If the current segment is larger than this value, then it increases
    # the threshold to 0.9 internally. After detecting this segment,
    # it resets the threshold to its original value.
    config.silero_vad.max_speech_duration = 8  # seconds
    config.sample_rate = sample_rate
    force_max_speech_duration = 20  # seconds

    window_size = config.silero_vad.window_size

    vad = sherpa_onnx.VoiceActivityDetector(config, buffer_size_in_seconds=100)

    print("识别已启动，请说话", file=sys.stderr)

    buffer = []

    # 创建进程间通信的队列和停止事件
    global samples_queue, stop_event, recording_process
    samples_queue = multiprocessing.Queue()
    stop_event = multiprocessing.Event()

    # 使用子进程而不是线程进行录音
    recording_process = multiprocessing.Process(
        target=start_recording,
        args=(selected_device_indices, samples_queue, stop_event, args.mix_mode, args.debug_save_audio)
    )
    recording_process.start()
    print(f"混音模式: {args.mix_mode}", file=sys.stderr)

    # display = sherpa_onnx.Display()
    printer = MyPrinter()

    started = False
    start_time = None
    last_update_time = None

    offset = 0
    while not killed:
        try:
            samples = samples_queue.get(timeout=0.5)  # 使用超时避免阻塞
            # 获取队列中所有已有的元素
            while not samples_queue.empty():
                try:
                    additional_samples = samples_queue.get_nowait()
                    samples = np.concatenate([samples, additional_samples])
                except:
                    break
        except:
            continue

        buffer = np.concatenate([buffer, samples])
        while offset + window_size < len(buffer):
            vad.accept_waveform(buffer[offset : offset + window_size])
            if not started and vad.is_speech_detected():
                started = True
                last_update_time = time.time()
                start_time = time.time()
            offset += window_size

        if not started:
            if len(buffer) > 10 * window_size:
                offset -= len(buffer) - 10 * window_size
                buffer = buffer[-10 * window_size :]

        if started and time.time() - last_update_time > 0.2:
            stream = recognizer.create_stream()
            stream.accept_waveform(sample_rate, buffer)
            recognizer.decode_stream(stream)
            text = stream.result.text.strip()
            if text:
                printer.do_print(text)
                # display.update_text(text)
                # display.display()

            last_update_time = time.time()

        while not vad.empty():
            # In general, this while loop is executed only once
            stream = recognizer.create_stream()
            stream.accept_waveform(sample_rate, vad.front.samples)

            vad.pop()
            recognizer.decode_stream(stream)

            text = stream.result.text.strip()

            # display.update_text(text)
            printer.do_print(text)

            buffer = []
            offset = 0
            started = False
            start_time = None

            # display.finalize_current_sentence()
            # display.display()
            printer.on_endpoint()

        if start_time and time.time() - start_time > force_max_speech_duration:
            print("大于强制截断时间！", file=sys.stderr)
            vad.reset()
            buffer = []
            offset = 0
            started = False
            start_time = None
            printer.on_endpoint()


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        killed = True
        cleanup_recording_process(stop_event, recording_process)
        print("\n检测到 Ctrl + C. 正在退出", file=sys.stderr)
