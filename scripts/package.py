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
PUBLISH_PROPS = [
    "-p:PublishSingleFile=true",
    "-p:DebugType=none",
]


def run(cmd: list[str], **kwargs) -> None:
    print(f"  {' '.join(cmd)}")
    result = subprocess.run(cmd, cwd=REPO_ROOT, **kwargs)
    if result.returncode != 0:
        sys.exit(result.returncode)


def restore(runtime_id: str) -> None:
    print("[1/5] Restoring packages...")
    run(["dotnet", "restore", "ExternalProgram.sln", "-r", runtime_id])


def build_swaddin(config: str) -> None:
    print("[2/5] Building SwAddin & Runtime...")
    run([
        "dotnet", "build",
        "ExternalProgram.SwAddin/ExternalProgram.SwAddin.csproj",
        "-c", config, "--no-restore", "-v", "minimal",
    ])
    run([
        "dotnet", "build",
        "ExternalProgram.SwAddin.Runtime/ExternalProgram.SwAddin.Runtime.csproj",
        "-c", config, "--no-restore", "-v", "minimal",
    ])


def publish_project(csproj: str, config: str, rid: str, output_dir: Path) -> None:
    run([
        "dotnet", "publish", csproj,
        "-c", config, "-r", rid,
        "--self-contained", "false",
        "-o", str(output_dir),
        "--no-restore", "-v", "minimal",
        *PUBLISH_PROPS,
    ])


def publish_all(config: str, rid: str, publish_dir: Path) -> None:
    print("[3/5] Publishing ExternalProgram & ReadBom...")
    publish_project("ExternalProgram.csproj", config, rid, publish_dir / "ExternalProgram")
    publish_project("ReadBom/ReadBom.csproj", config, rid, publish_dir / "ReadBom")


def copy_swaddin_runtime(config: str, publish_dir: Path) -> None:
    print("[4/5] Copying SwAddin Runtime...")
    src = REPO_ROOT / "ExternalProgram.SwAddin" / "bin" / config / "net48" / "Runtime"
    dst = publish_dir / "ExternalProgram" / "SwAddin" / "Runtime"
    if src.exists():
        if dst.exists():
            shutil.rmtree(dst)
        shutil.copytree(src, dst)
        print(f"       Runtime copied")
    else:
        print(f"       Runtime dir not found, skipping")


def download_runtime(version: str, publish_dir: Path) -> None:
    print(f"[5/5] .NET {version} Desktop Runtime installer...")
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
    parser.add_argument("--include-runtime", action="store_true",
                        help="Include .NET Desktop Runtime installer in package")
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
        publish_all(config, rid, publish_dir)

    copy_swaddin_runtime(config, publish_dir)

    if args.include_runtime:
        download_runtime(args.runtime_version, publish_dir)
    else:
        print("[5/5] Runtime skipped (use --include-runtime to add)")

    if not args.skip_zip:
        create_zip(args.zip_name, publish_dir)

    show_summary(publish_dir)


if __name__ == "__main__":
    main()
