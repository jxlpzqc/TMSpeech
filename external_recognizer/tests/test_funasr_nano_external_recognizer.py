import importlib.util
import io
import sys
import tempfile
import types
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from types import SimpleNamespace
from unittest import mock


EXTERNAL_RECOGNIZER_DIR = Path(__file__).resolve().parents[1]
SCRIPT_PATH = EXTERNAL_RECOGNIZER_DIR / "simulate-streaming-funasr-nano.py"


def make_common_audio_utils(sherpa_onnx=None):
    module = types.ModuleType("common_audio_utils")
    module.pyaudio = SimpleNamespace()
    module.sherpa_onnx = sherpa_onnx or SimpleNamespace()
    module.np = SimpleNamespace()
    module.sample_rate = 16000
    module.assert_file_exists = lambda path: None
    module.start_recording = lambda *args, **kwargs: None
    module.MyPrinter = type("MyPrinter", (), {})
    module.select_input_device = lambda devices, audio: []
    module.get_audio_devices = lambda audio: []
    module.cleanup_recording_process = lambda event, process: None
    return module


def load_script(common_audio_utils=None):
    common_audio_utils = common_audio_utils or make_common_audio_utils()
    spec = importlib.util.spec_from_file_location("funasr_nano_recognizer", SCRIPT_PATH)
    module = importlib.util.module_from_spec(spec)
    with mock.patch.dict(sys.modules, {"common_audio_utils": common_audio_utils}):
        spec.loader.exec_module(module)
    return module


def load_common_audio_utils():
    fake_modules = {
        "numpy": types.ModuleType("numpy"),
        "pyaudiowpatch": types.ModuleType("pyaudiowpatch"),
        "sherpa_onnx": types.ModuleType("sherpa_onnx"),
        "tkinter": types.ModuleType("tkinter"),
    }
    scipy = types.ModuleType("scipy")
    scipy.signal = types.ModuleType("signal")
    fake_modules["scipy"] = scipy
    spec = importlib.util.spec_from_file_location(
        "common_audio_utils_under_test",
        EXTERNAL_RECOGNIZER_DIR / "common_audio_utils.py",
    )
    module = importlib.util.module_from_spec(spec)
    with mock.patch.dict(sys.modules, fake_modules):
        spec.loader.exec_module(module)
    return module


class DependencyContractTests(unittest.TestCase):
    def test_common_audio_utils_installs_nano_capable_sherpa_onnx(self):
        source = (EXTERNAL_RECOGNIZER_DIR / "common_audio_utils.py").read_text(
            encoding="utf-8"
        )

        self.assertIn("sherpa_onnx==1.13.4", source)
        self.assertNotIn("sherpa_onnx==1.12.19", source)

    def test_printer_allows_identical_text_in_consecutive_sentences(self):
        module = load_common_audio_utils()
        output = io.StringIO()

        with redirect_stdout(output):
            printer = module.MyPrinter()
            printer.do_print("重复内容")
            printer.on_endpoint()
            printer.do_print("重复内容")
            printer.on_endpoint()

        self.assertEqual(output.getvalue(), "重复内容\n\n重复内容\n\n")


class CommandLineTests(unittest.TestCase):
    def test_defaults_point_to_the_official_int8_model_and_cpu(self):
        module = load_script()

        args = module.get_args([])

        self.assertEqual(args.provider, "cpu")
        self.assertTrue(args.itn)
        self.assertEqual(Path(args.encoder_adaptor).name, "encoder_adaptor.int8.onnx")
        self.assertEqual(Path(args.llm).name, "llm.int8.onnx")
        self.assertEqual(Path(args.embedding).name, "embedding.int8.onnx")
        self.assertEqual(Path(args.tokenizer).name, "Qwen3-0.6B")

    def test_cuda_language_and_no_itn_can_be_selected(self):
        module = load_script()

        args = module.get_args(
            ["--provider", "cuda", "--language", "Chinese", "--no-itn"]
        )

        self.assertEqual(args.provider, "cuda")
        self.assertEqual(args.language, "Chinese")
        self.assertFalse(args.itn)


class RecognizerTests(unittest.TestCase):
    def test_create_recognizer_forwards_all_nano_options(self):
        calls = []
        expected_recognizer = object()

        class OfflineRecognizer:
            @staticmethod
            def from_funasr_nano(**kwargs):
                calls.append(kwargs)
                return expected_recognizer

        sherpa_onnx = SimpleNamespace(OfflineRecognizer=OfflineRecognizer)
        module = load_script(make_common_audio_utils(sherpa_onnx))
        args = SimpleNamespace(
            encoder_adaptor="encoder.onnx",
            llm="llm.onnx",
            embedding="embedding.onnx",
            tokenizer="tokenizer",
            num_threads=4,
            provider="cuda",
            system_prompt="system",
            user_prompt="user",
            max_new_tokens=128,
            temperature=0.2,
            top_p=0.7,
            seed=7,
            language="Chinese",
            itn=False,
            hotwords="FunASR",
            debug=True,
        )

        recognizer = module.create_recognizer(args)

        self.assertIs(recognizer, expected_recognizer)
        self.assertEqual(
            calls,
            [
                {
                    "encoder_adaptor": "encoder.onnx",
                    "llm": "llm.onnx",
                    "embedding": "embedding.onnx",
                    "tokenizer": "tokenizer",
                    "num_threads": 4,
                    "sample_rate": 16000,
                    "feature_dim": 80,
                    "decoding_method": "greedy_search",
                    "debug": True,
                    "provider": "cuda",
                    "system_prompt": "system",
                    "user_prompt": "user",
                    "max_new_tokens": 128,
                    "temperature": 0.2,
                    "top_p": 0.7,
                    "seed": 7,
                    "language": "Chinese",
                    "itn": False,
                    "hotwords": "FunASR",
                }
            ],
        )

    def test_validate_model_paths_reports_every_missing_asset(self):
        module = load_script()
        args = SimpleNamespace(
            silero_vad_model="missing-vad.onnx",
            encoder_adaptor="missing-encoder.onnx",
            llm="missing-llm.onnx",
            embedding="missing-embedding.onnx",
            tokenizer="missing-tokenizer",
        )

        with self.assertRaises(FileNotFoundError) as context:
            module.validate_model_paths(args)

        message = str(context.exception)
        self.assertIn("missing-vad.onnx", message)
        self.assertIn("missing-encoder.onnx", message)
        self.assertIn("missing-llm.onnx", message)
        self.assertIn("missing-embedding.onnx", message)
        self.assertIn("missing-tokenizer", message)
        self.assertIn(module.FUNASR_NANO_MODEL_URL, message)

    def test_validate_model_paths_accepts_files_and_tokenizer_directory(self):
        module = load_script()
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            paths = {
                "silero_vad_model": root / "vad.onnx",
                "encoder_adaptor": root / "encoder.onnx",
                "llm": root / "llm.onnx",
                "embedding": root / "embedding.onnx",
            }
            for path in paths.values():
                path.touch()
            tokenizer = root / "tokenizer"
            tokenizer.mkdir()
            args = SimpleNamespace(
                **{key: str(value) for key, value in paths.items()},
                tokenizer=str(tokenizer),
            )

            module.validate_model_paths(args)

    def test_decode_samples_returns_trimmed_result(self):
        accepted = []
        stream = SimpleNamespace(
            result=SimpleNamespace(text="  你好，Fun-ASR-Nano  "),
            accept_waveform=lambda rate, samples: accepted.append((rate, samples)),
        )

        class Recognizer:
            def create_stream(self):
                return stream

            def decode_stream(self, actual_stream):
                self.decoded_stream = actual_stream

        recognizer = Recognizer()
        module = load_script()
        samples = [0.1, -0.1]

        text = module.decode_samples(recognizer, samples)

        self.assertEqual(text, "你好，Fun-ASR-Nano")
        self.assertEqual(accepted, [(16000, samples)])
        self.assertIs(recognizer.decoded_stream, stream)

    def test_drain_vad_segments_emits_only_completed_nonempty_sentences(self):
        segments = [
            SimpleNamespace(samples=[1.0]),
            SimpleNamespace(samples=[2.0]),
            SimpleNamespace(samples=[3.0]),
        ]

        class Vad:
            @property
            def front(self):
                return segments[0]

            def empty(self):
                return not segments

            def pop(self):
                segments.pop(0)

        results = iter(["第一句", "", "第二句"])

        class Recognizer:
            def create_stream(self):
                text = next(results)
                return SimpleNamespace(
                    result=SimpleNamespace(text=text),
                    accept_waveform=lambda rate, samples: None,
                )

            def decode_stream(self, stream):
                pass

        events = []
        printer = SimpleNamespace(
            do_print=lambda text: events.append(("text", text)),
            on_endpoint=lambda: events.append(("endpoint", None)),
        )
        module = load_script()

        count = module.drain_vad_segments(Vad(), Recognizer(), printer)

        self.assertEqual(count, 2)
        self.assertEqual(
            events,
            [
                ("text", "第一句"),
                ("endpoint", None),
                ("text", "第二句"),
                ("endpoint", None),
            ],
        )

    def test_numeric_validation_rejects_invalid_inference_settings(self):
        module = load_script()
        valid = {
            "num_threads": 4,
            "max_new_tokens": 128,
            "temperature": 1e-6,
            "top_p": 0.8,
            "vad_threshold": 0.5,
            "min_silence_duration": 0.5,
            "min_speech_duration": 0.25,
            "max_speech_duration": 20.0,
        }
        invalid_values = (
            ("num_threads", 0),
            ("max_new_tokens", 0),
            ("temperature", -0.1),
            ("top_p", 0.0),
            ("top_p", 1.1),
            ("vad_threshold", 0.0),
            ("min_silence_duration", 0.0),
            ("min_speech_duration", 0.0),
            ("max_speech_duration", 0.0),
        )

        for field, value in invalid_values:
            with self.subTest(field=field, value=value):
                values = dict(valid)
                values[field] = value
                with self.assertRaises(ValueError):
                    module.validate_numeric_args(SimpleNamespace(**values))

    def test_create_vad_forwards_segmentation_settings(self):
        created = []

        class VadModelConfig:
            def __init__(self):
                self.silero_vad = SimpleNamespace(window_size=512)
                self.sample_rate = None

        class VoiceActivityDetector:
            def __init__(self, config, buffer_size_in_seconds):
                created.append((config, buffer_size_in_seconds))

        sherpa_onnx = SimpleNamespace(
            VadModelConfig=VadModelConfig,
            VoiceActivityDetector=VoiceActivityDetector,
        )
        module = load_script(make_common_audio_utils(sherpa_onnx))
        args = SimpleNamespace(
            silero_vad_model="vad.onnx",
            vad_threshold=0.6,
            min_silence_duration=0.7,
            min_speech_duration=0.3,
            max_speech_duration=18.0,
        )

        vad, window_size = module.create_vad(args)

        self.assertIsInstance(vad, VoiceActivityDetector)
        self.assertEqual(window_size, 512)
        self.assertEqual(len(created), 1)
        config, buffer_size = created[0]
        self.assertEqual(config.silero_vad.model, "vad.onnx")
        self.assertEqual(config.silero_vad.threshold, 0.6)
        self.assertEqual(config.silero_vad.min_silence_duration, 0.7)
        self.assertEqual(config.silero_vad.min_speech_duration, 0.3)
        self.assertEqual(config.silero_vad.max_speech_duration, 18.0)
        self.assertEqual(config.sample_rate, 16000)
        self.assertEqual(buffer_size, 100)


if __name__ == "__main__":
    unittest.main()
