#!/usr/bin/env python3
"""Package ExternalProgram for release."""

import argparse
import json
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

import urllib.request

REPO_ROOT = Path(__file__).resolve().parent.parent


def run(cmd: list[str], **kwargs) -> None:
    print(f"  {' '.join(cmd)}")
    result = subprocess.run(cmd, cwd=REPO_ROOT, **kwargs)
    if result.returncode != 0:
        sys.exit(result.returncode)


def restore(runtime_id: str) -> None:
    print("[1/4] Restoring packages...")
    run(["dotnet", "restore", "ExternalProgram.sln", "-r", runtime_id])


def build_swaddin(config: str) -> None:
    print("[2/4] Building SwAddin...")
    run([
        "dotnet", "build",
        "ExternalProgram.SwAddin/ExternalProgram.SwAddin.csproj",
        "-c", config, "--no-restore", "-v", "minimal",
    ])


def build_project(csproj: str, config: str) -> None:
    run([
        "dotnet", "build", csproj,
        "-c", config, "--no-restore", "-v", "minimal",
    ])


def collect_build_output(csproj: str, config: str, output_dir: Path) -> None:
    """Copy build output (dll + all dependencies) from bin to output_dir."""
    proj_dir = Path(csproj).parent
    bin_dir = proj_dir / "bin" / config

    # Find the target framework subdirectory (e.g. net9.0-windows)
    tf_dirs = list(bin_dir.glob("net*"))
    if not tf_dirs:
        raise FileNotFoundError(f"No target framework dir found under {bin_dir}")
    src = tf_dirs[0]

    output_dir.mkdir(parents=True, exist_ok=True)
    for f in src.iterdir():
        if f.is_file():
            shutil.copy2(f, output_dir / f.name)

    print(f"       {len(list(src.iterdir()))} files → {output_dir}")


def build_all(config: str, rid: str, publish_dir: Path) -> None:
    print("[3/4] Building ExternalProgram & ReadBom...")
    build_project("ExternalProgram.csproj", config)
    build_project("ReadBom/ReadBom.csproj", config)

    collect_build_output("ExternalProgram.csproj", config, publish_dir / "ExternalProgram")
    collect_build_output("ReadBom/ReadBom.csproj", config, publish_dir / "ReadBom")

    # Copy SwAddin DLLs (handled by ExternalProgram.csproj's CopySwAddinToOutput target
    # during build; need to also copy the SwAddin output dir to publish)
    swaddin_src = REPO_ROOT / "ExternalProgram.SwAddin" / "bin" / config / "net48"
    swaddin_dst = publish_dir / "ExternalProgram" / "SwAddin"
    swaddin_dst.mkdir(parents=True, exist_ok=True)
    for f in swaddin_src.iterdir():
        if f.is_file():
            shutil.copy2(f, swaddin_dst / f.name)
    print(f"       SwAddin DLLs → {swaddin_dst}")


def download_runtime(version: str, publish_dir: Path) -> None:
    print(f"[4/4] .NET {version} Desktop Runtime installer...")
    cache_dir = REPO_ROOT / ".cache"
    cache_dir.mkdir(exist_ok=True)

    installer_name = f"dotnet-runtime-{version}-win-x64.exe"
    cache_path = cache_dir / installer_name

    if cache_path.exists():
        print(f"       Using cached: .cache/{installer_name}")
    else:
        channel = version[: version.rindex(".")]
        url = None
        try:
            releases_url = f"https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/{channel}/releases.json"
            with urllib.request.urlopen(releases_url, timeout=15) as resp:
                data = json.loads(resp.read())
            for rel in data.get("releases", []):
                if rel.get("runtime", {}).get("version") == version:
                    for f in rel["runtime"].get("files", []):
                        if "windowsdesktop-runtime" in f["name"] and f["name"].endswith("win-x64.exe"):
                            url = f["url"]
                            break
                    break
            if not url:
                raise Exception("Installer not found in release metadata")
        except Exception as e:
            print(f"       Release JSON lookup failed: {e}")
            print("       Falling back to direct CDN URL...")
            url = f"https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/{version}/windowsdesktop-runtime-{version}-win-x64.exe"

        print(f"       Downloading to .cache/...", end="", flush=True)
        urllib.request.urlretrieve(url, cache_path)
        print(" done")

    dst = publish_dir / installer_name
    shutil.copy2(cache_path, dst)


def create_zip(zip_name: str, publish_dir: Path) -> None:
    print(f"\n[zip] Creating {zip_name}.zip...")
    zip_path = REPO_ROOT / f"{zip_name}.zip"
    if zip_path.exists():
        zip_path.unlink()

    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for file in sorted(publish_dir.rglob("*")):
            if file.is_file():
                arcname = str(file.relative_to(publish_dir.parent))
                zf.write(file, arcname)

    size_mb = zip_path.stat().st_size / (1024 * 1024)
    print(f"       {zip_path}  ({size_mb:.1f} MB)")


def show_summary(publish_dir: Path) -> None:
    print(f"\n=== Done ===")
    print(f"Output: {publish_dir}")
    for file in sorted(publish_dir.rglob("*")):
        if file.is_file():
            size_kb = file.stat().st_size / 1024
            rel = file.relative_to(publish_dir)
            print(f"  {size_kb:7.0f} KB  {rel}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Package ExternalProgram for release")
    parser.add_argument("-c", "--configuration", default="Release",
                        choices=["Release", "Debug"])
    parser.add_argument("-o", "--output", default="publish")
    parser.add_argument("--skip-build", action="store_true")
    parser.add_argument("--skip-runtime", action="store_true",
                        help="Skip .NET Desktop Runtime installer")
    parser.add_argument("--skip-zip", action="store_true")
    parser.add_argument("--runtime-version", default="9.0.8")
    parser.add_argument("--zip-name", default="ExternalProgram")
    parser.add_argument("-r", "--rid", default="win-x64")
    args = parser.parse_args()

    config = args.configuration
    rid = args.rid
    publish_dir = REPO_ROOT / args.output

    print(f"=== Package ExternalProgram {config} ===")

    if not args.skip_build:
        if publish_dir.exists():
            shutil.rmtree(publish_dir)
            print(f"[clean] Removed {publish_dir}")

        restore(rid)
        build_swaddin(config)
        build_all(config, rid, publish_dir)

    if args.skip_runtime:
        print("[4/4] Runtime skipped (--skip-runtime)")
    else:
        download_runtime(args.runtime_version, publish_dir)

    if not args.skip_zip:
        create_zip(args.zip_name, publish_dir)

    show_summary(publish_dir)


if __name__ == "__main__":
    main()
