#!/usr/bin/env python3
#
# Copyright (c)  2025  Xiaomi Corporation

"""
This file demonstrates how to use sherpa-onnx Python APIs
with streaming OnlineRecognizer for real-time speech recognition
from a microphone with multi-device support.

Requirements:

wget https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20.tar.bz2

Usage:

python simulate-streaming.py
"""
import argparse
import sys
import multiprocessing
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

# 这里已经改了
model_url = 'https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20.tar.bz2'
script_parent_dir = os.path.dirname(script_dir)
model_path = os.path.join(script_parent_dir, "models")
encoder_path = os.path.join(model_path, "encoder.onnx")
decoder_path = os.path.join(model_path, "decoder.onnx")
joiner_path = os.path.join(model_path, "joiner.onnx")
tokens_txt_path = os.path.join(model_path, "tokens.txt")

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
        "--tokens",
        type=str,
        default=tokens_txt_path,
        help="Path to tokens.txt",
    )

    parser.add_argument(
        "--encoder",
        type=str,
        default=encoder_path,
        help="Path to the encoder model",
    )

    parser.add_argument(
        "--decoder",
        type=str,
        default=decoder_path,
        help="Path to the decoder model",
    )

    parser.add_argument(
        "--joiner",
        type=str,
        default=joiner_path,
        help="Path to the joiner model",
    )

    parser.add_argument(
        "--decoding-method",
        type=str,
        default="greedy_search",
        help="Valid values are greedy_search and modified_beam_search",
    )

    parser.add_argument(
        "--provider",
        type=str,
        default="cpu",
        help="Valid values: cpu, cuda, coreml",
    )

    parser.add_argument(
        "--hotwords-file",
        type=str,
        default="",
        help="""
        The file containing hotwords, one words/phrases per line, and for each
        phrase the bpe/cjkchar are separated by a space. For example:

        ▁HE LL O ▁WORLD
        你 好 世 界
        """,
    )

    parser.add_argument(
        "--hotwords-score",
        type=float,
        default=1.5,
        help="""
        The hotword score of each token for biasing word/phrase. Used only if
        --hotwords-file is given.
        """,
    )

    parser.add_argument(
        "--blank-penalty",
        type=float,
        default=0.0,
        help="""
        The penalty applied on blank symbol during decoding.
        Note: It is a positive value that would be applied to logits like
        this `logits[:, 0] -= blank_penalty` (suppose logits.shape is
        [batch_size, vocab] and blank id is 0).
        """,
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
        "--device",
        type=int,
        default=-1,
        help="Device index to use for recording. -1 means show device selection dialog",
    )

    parser.add_argument(
        "--mix-mode",
        type=str,
        default="average",
        help="Mixing mode for multi-device recording: 'average' or 'add'",
    )

    parser.add_argument(
        "--debug-save-audio",
        type=str,
        default="",
        help="If not empty, save mixed audio to this WAV file path for debugging",
    )

    parser.add_argument(
        "--num-threads",
        type=int,
        default=1,
        help="Number of threads for recognition",
    )

    return parser.parse_args()


def try_download_model(model_paths):
    for p in model_paths:
        if not os.path.exists(p):
            print(f"请下载文件到：{p}", file=sys.stderr)
            print(f"下载链接：{model_url}", file=sys.stderr)

def create_recognizer(args):
    assert_file_exists(args.encoder)
    assert_file_exists(args.decoder)
    assert_file_exists(args.joiner)
    assert_file_exists(args.tokens)

    recognizer = sherpa_onnx.OnlineRecognizer.from_transducer(
        tokens=args.tokens,
        encoder=args.encoder,
        decoder=args.decoder,
        joiner=args.joiner,
        num_threads=args.num_threads,
        sample_rate=16000,
        feature_dim=80,
        enable_endpoint_detection=True,
        rule1_min_trailing_silence=2.4,
        rule2_min_trailing_silence=1.2,
        rule3_min_utterance_length=300,  # it essentially disables this rule
        decoding_method=args.decoding_method,
        provider=args.provider,
        hotwords_file=args.hotwords_file,
        hotwords_score=args.hotwords_score,
        blank_penalty=args.blank_penalty,
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

    try_download_model([args.tokens, args.encoder, args.decoder, args.joiner])

    assert args.num_threads > 0, args.num_threads

    print("正在启动识别器，请稍后", file=sys.stderr)
    recognizer = create_recognizer(args)

    print("识别已启动，请说话", file=sys.stderr)

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

    stream = recognizer.create_stream()
    while not killed:
        try:
            samples = samples_queue.get(timeout=0.5)  # 使用超时避免阻塞
        except:
            continue

        # 将音频数据送入识别流
        stream.accept_waveform(sample_rate, samples)

        # 处理所有准备好的音频
        while recognizer.is_ready(stream):
            recognizer.decode_stream(stream)

        # 检查是否到达端点
        is_endpoint = recognizer.is_endpoint(stream)

        # 获取识别结果
        text = recognizer.get_result(stream).strip()

        # 显示结果
        # display.update_text(result)
        # display.display()
        printer.do_print(text)

        # 如果到达端点，完成当前句子并重置流
        if is_endpoint:
            if text:
                # display.finalize_current_sentence()
                # display.display()
                printer.on_endpoint()

            recognizer.reset(stream)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        killed = True
        cleanup_recording_process(stop_event, recording_process)
        print("\n检测到 Ctrl + C. 正在退出", file=sys.stderr)
