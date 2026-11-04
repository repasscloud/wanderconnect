"""Q129 / D320: release version allocation, exercised in a throwaway git repository (never this repository's tags)."""
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]


class ReleaseVersion(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        (self.dir / "scripts").mkdir()
        shutil.copy(REPO / "scripts/release_version.py", self.dir / "scripts/release_version.py")
        shutil.copy(REPO / "Directory.Build.props", self.dir / "Directory.Build.props")
        theme = self.dir / "containers/keycloak/themes/swoovr/login"
        theme.mkdir(parents=True)
        shutil.copy(REPO / "containers/keycloak/themes/swoovr/login/theme.properties", theme / "theme.properties")
        for args in (["init", "-q"], ["config", "user.email", "fictional@example.test"], ["config", "user.name", "Fictional"],
                     ["config", "commit.gpgsign", "false"], ["config", "tag.gpgsign", "false"], ["add", "."], ["commit", "-qm", "init"]):
            subprocess.run(["git", "-C", str(self.dir), *args], check=True, capture_output=True)

    def tearDown(self):
        shutil.rmtree(self.dir)

    def run_script(self, *args, ok=True):
        result = subprocess.run(["python3", str(self.dir / "scripts/release_version.py"), *args], capture_output=True, text=True)
        if ok: self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout.strip(), result

    def commit(self):
        subprocess.run(["git", "-C", str(self.dir), "commit", "-qam", "change", "--allow-empty"], check=True, capture_output=True)

    def test_first_release_then_builds_increment(self):
        self.assertEqual(self.run_script("show")[0], "1.0.0")
        self.assertEqual(self.run_script("next")[0], "1.0.0")
        self.assertEqual(self.run_script("allocate")[0], "1.0.0")
        self.commit()
        self.assertEqual(self.run_script("next")[0], "1.0.1")
        self.assertEqual(self.run_script("allocate")[0], "1.0.1")

    def test_untagged_builds_never_advance_and_rebuilds_keep_their_version(self):
        self.run_script("allocate"); self.commit(); self.run_script("allocate")      # 1.0.0, 1.0.1
        self.assertEqual(self.run_script("for-head")[0], "1.0.1")
        self.commit()
        self.assertEqual(self.run_script("for-head")[0], "1.0.0", "an untagged build reports the committed version")
        self.assertEqual(self.run_script("next")[0], "1.0.2", "asking does not allocate")
        self.assertEqual(self.run_script("next")[0], "1.0.2")
        subprocess.run(["git", "-C", str(self.dir), "checkout", "-q", "v1.0.1"], check=True)
        self.assertEqual(self.run_script("for-head")[0], "1.0.1", "rebuilding a release keeps its version")
        self.assertIn("-p:SwoovrVersionBuild=1 ", self.run_script("msbuild-args")[0])

    def test_bumps_reset_lower_parts(self):
        self.run_script("allocate"); self.commit(); self.run_script("allocate")
        self.assertEqual(self.run_script("bump", "minor")[0], "1.1.0"); self.commit()
        self.assertEqual(self.run_script("next")[0], "1.1.0")
        self.assertIn("swoovrVersion=1.1.0", (self.dir / "containers/keycloak/themes/swoovr/login/theme.properties").read_text())
        self.assertEqual(self.run_script("bump", "major")[0], "2.0.0"); self.commit()
        self.assertEqual(self.run_script("next")[0], "2.0.0")
        self.assertIn("<SwoovrVersionMinor>0</SwoovrVersionMinor>", (self.dir / "Directory.Build.props").read_text())

    def test_a_version_is_never_allocated_twice_or_mismatched(self):
        self.run_script("allocate")
        subprocess.run(["git", "-C", str(self.dir), "tag", "-a", "v1.0.1", "-m", "concurrent"], check=True, capture_output=True)
        self.assertEqual(self.run_script("next")[0], "1.0.2", "a concurrently created tag is respected")
        self.assertNotEqual(self.run_script("msbuild-args", "2.0.0", ok=False)[1].returncode, 0, "other Major.Minor refused")


if __name__ == "__main__":
    unittest.main()
