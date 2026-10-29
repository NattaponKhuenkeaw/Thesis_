"""Package actual Unity render readbacks; no game asset generation."""
from pathlib import Path
import json
import subprocess
import imageio_ffmpeg
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
report = json.loads((ROOT / "Runtime/runtime-report.json").read_text())
assert report["finished"] and not report["error"]
selected = [run for run in report["runs"] if run["requestedFps"] == 60]
manifest = []
for run in selected:
    folder = ROOT / "Runtime" / f"60fps_{run['requestedFacing']}"
    for index, frame in enumerate(run["frames"]):
        path = folder / f"frame{index:04}.png"
        raw = folder / f"frame{index:04}.rgb"
        if raw.exists():
            Image.frombytes("RGB", (960,540), raw.read_bytes()).transpose(Image.Transpose.FLIP_TOP_BOTTOM).save(path)
        duration = run["frames"][index+1]["realtime"] - frame["realtime"] if index+1 < len(run["frames"]) else .3
        manifest.extend([f"file '{path.as_posix()}'",f"duration {max(.001,duration):.6f}"])
manifest.append(f"file '{path.as_posix()}'")
concat = ROOT / "Runtime/review.ffconcat"
concat.write_text("\n".join(manifest)+"\n")
subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),"-hide_banner","-loglevel","error","-y","-f","concat","-safe","0",
    "-i",str(concat),"-an","-vsync","vfr","-c:v","libx264","-crf","18","-pix_fmt","yuv420p","-movflags","+faststart",
    str(ROOT / "dimension-slash-procedural-silent.mp4")],check=True)

run = selected[0]
folder = ROOT / "Runtime/60fps_1"
board = Image.new("RGB",(1440,604),"#151020")
draw = ImageDraw.Draw(board)
for index,(step,frame,label) in enumerate([
    ("DimensionSlash",9,"Anticipation / lunge"),("DimensionSlash",29,"Sliding entry"),
    ("DimensionSlash",58,"Overlapping field"),("DimensionSlash",91,"Reappear above field"),
    ("DimensionSlash",100,"Plunge"),("DimensionSlashFinalCut",14,"Existing Dimension Cut")]):
    candidate = min((i for i,f in enumerate(run["frames"]) if f["step"] == step),key=lambda i:abs(run["frames"][i]["logicFrame"]-frame))
    actual = run["frames"][candidate]
    picture = Image.open(folder / f"frame{candidate:04}.png")
    x,y=index%3*480,index//3*302
    board.paste(picture.resize((480,270),Image.Resampling.NEAREST),(x,y+32))
    draw.text((x+10,y+10),f"{label} | {actual['logicFrame']}",fill="#e9d7ff")
board.save(ROOT / "procedural-phase-board.png")
summary = {"startedUtc":report["startedUtc"],"finished":report["finished"],"pausePreservedWindow":report["pausePreservedWindow"],
    "expiredWindowFallsBack":report["expiredWindowFallsBack"],"startupDamageReleasesCamera":report["startupDamageReleasesCamera"],
    "hiddenActorRestoredOnDisable":report["hiddenActorRestoredOnDisable"],
    "runs":[{"fpsCap":r["requestedFps"],"facing":r["requestedFacing"],"primaryContacts":len(r["contacts"]),
        "barrageContacts":sum(c["step"] == "DimensionSlash" for c in r["contacts"]),
        "finalContacts":sum(c["step"] == "DimensionSlashFinalCut" for c in r["contacts"]),
        "criticalContacts":sum(c["critical"] for c in r["contacts"]),"minimumTimeScale":r["minimumTimeScale"],
        "actorRestored":r["actorRestored"],"observedFps":r["observedFps"],"ended":r["ended"],
        "foregroundCuts":max(f["foregroundCuts"] for f in r["frames"]),"totalCuts":max(f["totalCuts"] for f in r["frames"]),
        "armedAfterFive":r["armedAfterFive"],"consumed":r["consumed"],"cameraReleased":r["cameraReleased"]} for r in report["runs"]]}
(ROOT/"verification-summary.json").write_text(json.dumps(summary,indent=2))
print(json.dumps(summary,indent=2))
