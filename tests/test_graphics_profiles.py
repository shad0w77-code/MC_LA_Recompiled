import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class GraphicsProfilesTests(unittest.TestCase):
    def test_native_and_launcher_use_identical_profile_values(self):
        spec = importlib.util.spec_from_file_location("graphics_profiles", ROOT / "scripts/generate_graphics_profiles.py")
        generator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(generator)
        profiles = json.loads((ROOT / "config/graphics_profiles.json").read_text(encoding="utf8"))
        self.assertEqual(generator.render(profiles), (ROOT / "src/graphics_profiles.h").read_text(encoding="ascii"))


if __name__ == "__main__":
    unittest.main()
