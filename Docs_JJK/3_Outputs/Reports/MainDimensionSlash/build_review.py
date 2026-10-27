"""Package measured Unity captures for review; does not author game art."""
import json
from pathlib import Path
import subprocess
import imageio_ffmpeg
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
report = json.loads((ROOT / "Runtime/runtime-report.json").read_text())
assert report["finished"] and not report["error"], "Wait for the Unity capture to finish."
runs = [run for run in report["runs"] if run["requestedFps"] == 60]
lines = []
for run in runs:
    frames = run["frames"]
    folder = ROOT / "Runtime" / f"60fps_{run['requestedFacing']}"
    for i, frame in enumerate(frames):
        path = (folder / f"frame{i:04}.png").as_posix()
        duration = frames[i + 1]["realtime"] - frame["realtime"] if i + 1 < len(frames) else 0.35
        lines.extend([f"file '{path}'", f"duration {max(0.001, duration):.6f}"])
lines.append(f"file '{path}'")
manifest = ROOT / "Runtime/review-frames.ffconcat"
manifest.write_text("\n".join(lines) + "\n")
video = ROOT / "dimension-slash-gameplay-silent.mp4"
subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-hide_banner", "-loglevel", "error", "-y",
    "-f", "concat", "-safe", "0", "-i", str(manifest), "-an", "-vsync", "vfr",
    "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(video)], check=True)

run = runs[0]
folder = ROOT / "Runtime" / f"60fps_{run['requestedFacing']}"
board = Image.new("RGB", (1440, 604), "#10121c")
draw = ImageDraw.Draw(board)
for i, (requested, phase) in enumerate([(12,"First burst"),(27,"Accelerating burst"),(50,"Compression"),(54,"Final contact"),(60,"Decay"),(80,"Recovery")]):
    index = min(range(len(run["frames"])), key=lambda j: abs(run["frames"][j]["logicFrame"] - requested))
    frame = run["frames"][index]
    image = Image.open(folder / f"frame{index:04}.png").convert("RGB")
    x, y = i % 3 * 480, i // 3 * 302
    board.paste(image.resize((480,270),Image.Resampling.NEAREST),(x,y+32))
    draw.text((x+12,y+9),f"{phase} | captured logic frame {frame['logicFrame']}",fill="#e6dcff")
board.save(ROOT / "dimension-slash-gameplay-contact-sheet.png")
print(json.dumps({"video":str(video),"contactSheet":str(ROOT / "dimension-slash-gameplay-contact-sheet.png"),
    "note":"Silent live Unity footage; timestamps retain capture overhead and hitstop."},indent=2))
