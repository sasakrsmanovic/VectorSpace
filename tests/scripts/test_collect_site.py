"""Dependency-free regression tests for static-site publication metadata."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "collect-site.py"
spec = importlib.util.spec_from_file_location("collect_site", SCRIPT)
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)


class CollectSiteTests(unittest.TestCase):
    def test_project_version(self):
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory) / "Directory.Build.props"
            project.write_text("<Project><PropertyGroup><Version> 1.2.3-alpha.4 </Version></PropertyGroup></Project>")
            self.assertEqual(collector.project_version(project), "1.2.3-alpha.4")

    def test_missing_or_unresolved_version_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory) / "Directory.Build.props"
            for value in ("", "$(OtherVersion)"):
                project.write_text(f"<Project><PropertyGroup><Version>{value}</Version></PropertyGroup></Project>")
                with self.assertRaises(ValueError):
                    collector.project_version(project)

    def test_collection_preserves_assets_and_sets_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); source = root / "publish" / "wwwroot"; source.mkdir(parents=True)
            (source / "index.html").write_text("Uno app")
            (source / ".hidden").write_text("asset")
            collector.collect_site(root / "publish", root / "site", "2.3.4", "source-sha")
            self.assertEqual((root / "site" / ".hidden").read_text(), "asset")
            self.assertTrue((root / "site" / ".nojekyll").exists())
            self.assertEqual(json.loads((root / "site" / "build-info.json").read_text())["commit"], "source-sha")
            self.assertEqual(json.loads((root / "site" / "build-info.json").read_text())["version"], "2.3.4")

    def test_invalid_input_does_not_create_output(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for version in ("", "1.0.0"):
                with self.assertRaises(ValueError):
                    collector.collect_site(root, root / "site", version, "local")
                self.assertFalse((root / "site").exists())

    def test_source_output_overlap_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); source = root / "publish"; source.mkdir(); (source / "index.html").touch()
            for destination in (root, source, source / "site"):
                with self.assertRaises(ValueError):
                    collector.collect_site(source, destination, "1.0.0", "local")

    def test_cli_version_precedence_and_external_working_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); source = root / "publish"; source.mkdir(); (source / "index.html").touch()
            env = dict(os.environ); env.pop("VERSION", None); env["GITHUB_SHA"] = "build-sha"
            expected = collector.project_version(ROOT / "Directory.Build.props")
            for index, (override, environment_version, version) in enumerate(((None, None, expected), (None, "9.0.0-rc.1", "9.0.0-rc.1"), ("10.0.0", "9.0.0", "10.0.0"))):
                if environment_version is not None:
                    env["VERSION"] = environment_version
                destination = root / f"site-{index}"
                command = [sys.executable, str(SCRIPT), str(source), str(destination)]
                if override is not None:
                    command += ["--version", override]
                subprocess.run(command, cwd=root, env=env, check=True, capture_output=True)
                metadata = json.loads((destination / "build-info.json").read_text())
                self.assertEqual(metadata["version"], version)
                self.assertEqual(metadata["commit"], "build-sha")


if __name__ == "__main__":
    unittest.main()
