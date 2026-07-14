#!/usr/bin/env python3
#
# Copyright (c) 2025 Xiaomi Corporation

"""Use Fun-ASR-Nano as a TMSpeech command recognizer."""

import argparse
import multiprocessing
import queue
import sys
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))

from common_audio_utils import (  # noqa: E402
    MyPrinter,
    cleanup_recording_process,
    get_audio_devices,
    np,
    pyaudio,
    sample_rate,
    select_input_device,
    sherpa_onnx,
    start_recording,
)


FUNASR_NANO_MODEL_NAME = "sherpa-onnx-funasr-nano-int8-2025-12-30"
FUNASR_NANO_MODEL_URL = (
    "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/"
    f"{FUNASR_NANO_MODEL_NAME}.tar.bz2"
)
SILERO_VAD_MODEL_URL = (
    "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.onnx"
)
DEFAULT_MODEL_DIR = SCRIPT_DIR / FUNASR_NANO_MODEL_NAME


def get_args(argv=None):
    parser = argparse.ArgumentParser(
        formatter_class=argparse.ArgumentDefaultsHelpFormatter
    )
    parser.add_argument("--device", type=int, default=-1, help="输入设备编号")
    parser.add_argument(
        "--silero-vad-model",
        default=str(SCRIPT_DIR / "silero_vad.onnx"),
        help="silero_vad.onnx 文件路径",
    )
    parser.add_argument(
        "--encoder-adaptor",
        default=str(DEFAULT_MODEL_DIR / "encoder_adaptor.int8.onnx"),
        help="Fun-ASR-Nano encoder adaptor ONNX 文件",
    )
    parser.add_argument(
        "--llm",
        default=str(DEFAULT_MODEL_DIR / "llm.int8.onnx"),
        help="Fun-ASR-Nano LLM ONNX 文件",
    )
    parser.add_argument(
        "--embedding",
        default=str(DEFAULT_MODEL_DIR / "embedding.int8.onnx"),
        help="Fun-ASR-Nano embedding ONNX 文件",
    )
    parser.add_argument(
        "--tokenizer",
        default=str(DEFAULT_MODEL_DIR / "Qwen3-0.6B"),
        help="Qwen3 tokenizer 目录",
    )
    parser.add_argument("--num-threads", type=int, default=4, help="推理线程数")
    parser.add_argument(
        "--provider",
        choices=("cpu", "cuda"),
        default="cpu",
        help="ONNX Runtime 执行后端",
    )
    parser.add_argument(
        "--system-prompt",
        default="You are a helpful assistant.",
        help="Fun-ASR-Nano system prompt",
    )
    parser.add_argument(
        "--user-prompt",
        default="语音转写:",
        help="Fun-ASR-Nano user prompt",
    )
    parser.add_argument(
        "--max-new-tokens", type=int, default=512, help="单段最多生成 token 数"
    )
    parser.add_argument("--temperature", type=float, default=1e-6)
    parser.add_argument("--top-p", type=float, default=0.8)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument(
        "--language",
        default="",
        help="模型支持的语言名称；留空使用 checkpoint 默认行为",
    )
    parser.add_argument("--itn", dest="itn", action="store_true", help="启用 ITN")
    parser.add_argument("--no-itn", dest="itn", action="store_false", help="关闭 ITN")
    parser.set_defaults(itn=True)
    parser.add_argument("--hotwords", default="", help="逗号分隔的热词")
    parser.add_argument(
        "--debug", action="store_true", help="输出 sherpa-onnx 调试信息"
    )
    parser.add_argument(
        "--vad-threshold", type=float, default=0.5, help="Silero VAD 阈值"
    )
    parser.add_argument(
        "--min-silence-duration",
        type=float,
        default=0.5,
        help="结束语音段所需的最短静音秒数",
    )
    parser.add_argument(
        "--min-speech-duration",
        type=float,
        default=0.25,
        help="保留语音段所需的最短秒数",
    )
    parser.add_argument(
        "--max-speech-duration",
        type=float,
        default=20.0,
        help="单个语音段的最长秒数",
    )
    parser.add_argument(
        "--mix-mode",
        choices=("average", "add"),
        default="average",
        help="多设备混音模式",
    )
    parser.add_argument(
        "--debug-save-audio",
        default="",
        help="将混音后的音频保存到指定 WAV 文件",
    )
    return parser.parse_args(argv)


def validate_model_paths(args):
    required_files = (
        args.silero_vad_model,
        args.encoder_adaptor,
        args.llm,
        args.embedding,
    )
    missing = [str(path) for path in required_files if not Path(path).is_file()]
    if not Path(args.tokenizer).is_dir():
        missing.append(str(args.tokenizer))

    if missing:
        details = "\n".join(f"  - {path}" for path in missing)
        raise FileNotFoundError(
            "缺少 Fun-ASR-Nano 运行文件:\n"
            f"{details}\n"
            f"Nano 模型: {FUNASR_NANO_MODEL_URL}\n"
            f"Silero VAD: {SILERO_VAD_MODEL_URL}"
        )


def validate_numeric_args(args):
    if args.num_threads <= 0:
        raise ValueError("--num-threads 必须大于 0")
    if args.max_new_tokens <= 0:
        raise ValueError("--max-new-tokens 必须大于 0")
    if args.temperature < 0:
        raise ValueError("--temperature 不能小于 0")
    if not 0 < args.top_p <= 1:
        raise ValueError("--top-p 必须大于 0 且不超过 1")
    if not 0 < args.vad_threshold < 1:
        raise ValueError("--vad-threshold 必须在 0 和 1 之间")
    for name in (
        "min_silence_duration",
        "min_speech_duration",
        "max_speech_duration",
    ):
        if getattr(args, name) <= 0:
            raise ValueError(f"--{name.replace('_', '-')} 必须大于 0")


def create_recognizer(args):
    return sherpa_onnx.OfflineRecognizer.from_funasr_nano(
        encoder_adaptor=args.encoder_adaptor,
        llm=args.llm,
        embedding=args.embedding,
        tokenizer=args.tokenizer,
        num_threads=args.num_threads,
        sample_rate=sample_rate,
        feature_dim=80,
        decoding_method="greedy_search",
        debug=args.debug,
        provider=args.provider,
        system_prompt=args.system_prompt,
        user_prompt=args.user_prompt,
        max_new_tokens=args.max_new_tokens,
        temperature=args.temperature,
        top_p=args.top_p,
        seed=args.seed,
        language=args.language,
        itn=args.itn,
        hotwords=args.hotwords,
    )


def create_vad(args):
    config = sherpa_onnx.VadModelConfig()
    config.silero_vad.model = args.silero_vad_model
    config.silero_vad.threshold = args.vad_threshold
    config.silero_vad.min_silence_duration = args.min_silence_duration
    config.silero_vad.min_speech_duration = args.min_speech_duration
    config.silero_vad.max_speech_duration = args.max_speech_duration
    config.sample_rate = sample_rate
    window_size = config.silero_vad.window_size
    vad = sherpa_onnx.VoiceActivityDetector(config, buffer_size_in_seconds=100)
    return vad, window_size


def decode_samples(recognizer, samples):
    stream = recognizer.create_stream()
    stream.accept_waveform(sample_rate, samples)
    recognizer.decode_stream(stream)
    return stream.result.text.strip()


def drain_vad_segments(vad, recognizer, printer):
    emitted = 0
    while not vad.empty():
        samples = vad.front.samples
        vad.pop()
        text = decode_samples(recognizer, samples)
        if text:
            printer.do_print(text)
            printer.on_endpoint()
            emitted += 1
    return emitted


def read_audio_batch(samples_queue):
    chunks = [samples_queue.get(timeout=0.5)]
    while True:
        try:
            chunks.append(samples_queue.get_nowait())
        except queue.Empty:
            break
    if len(chunks) == 1:
        return np.asarray(chunks[0], dtype=np.float32)
    return np.concatenate(chunks).astype(np.float32, copy=False)


def select_devices(args):
    audio = pyaudio.PyAudio()
    try:
        devices = get_audio_devices(audio)
        if not devices:
            raise RuntimeError("没有任何输入设备")

        print("可用设备:", file=sys.stderr)
        for index, device in devices:
            print(
                f"  {index}: {device['name']} "
                f"(输入: {device['maxInputChannels']}, 输出: {device['maxOutputChannels']})",
                file=sys.stderr,
            )

        if args.device >= 0:
            selected = [args.device]
        else:
            selected = select_input_device(devices, audio)
            if not selected:
                selected = [audio.get_default_input_device_info()["index"]]
        return devices, selected
    finally:
        audio.terminate()


def run_recognition(args, recognizer, vad, window_size, selected_devices):
    samples_queue = multiprocessing.Queue()
    stop_event = multiprocessing.Event()
    recording_process = multiprocessing.Process(
        target=start_recording,
        args=(
            selected_devices,
            samples_queue,
            stop_event,
            args.mix_mode,
            args.debug_save_audio,
        ),
    )
    recording_process.start()
    printer = MyPrinter()
    buffered = np.empty(0, dtype=np.float32)

    try:
        while True:
            try:
                samples = read_audio_batch(samples_queue)
            except queue.Empty:
                continue

            if buffered.size:
                samples = np.concatenate((buffered, samples))

            complete_length = (len(samples) // window_size) * window_size
            for offset in range(0, complete_length, window_size):
                vad.accept_waveform(samples[offset : offset + window_size])
            buffered = samples[complete_length:]
            drain_vad_segments(vad, recognizer, printer)
    finally:
        cleanup_recording_process(stop_event, recording_process)


def main(argv=None):
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")

    args = get_args(argv)
    validate_numeric_args(args)
    validate_model_paths(args)
    devices, selected_devices = select_devices(args)

    names = {index: device["name"] for index, device in devices}
    print(
        "使用输入设备: "
        + ", ".join(
            f"{index}: {names.get(index, '未知设备')}" for index in selected_devices
        ),
        file=sys.stderr,
    )
    print(
        f"正在加载 Fun-ASR-Nano ({args.provider}, {args.num_threads} threads)",
        file=sys.stderr,
    )
    recognizer = create_recognizer(args)
    vad, window_size = create_vad(args)
    print("识别已启动，请说话", file=sys.stderr)
    run_recognition(args, recognizer, vad, window_size, selected_devices)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n检测到 Ctrl+C，正在退出", file=sys.stderr)
    except (FileNotFoundError, RuntimeError, ValueError) as error:
        print(f"启动失败: {error}", file=sys.stderr)
        raise SystemExit(1)
