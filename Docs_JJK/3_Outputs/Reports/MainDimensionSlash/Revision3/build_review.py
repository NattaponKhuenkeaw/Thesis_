"""Package Unity camera readbacks without changing game assets."""
from pathlib import Path
import json
import subprocess
import imageio_ffmpeg
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
report = json.loads((ROOT / "Runtime/runtime-report.json").read_text())
assert report["finished"] and not report["error"]

def folder_for(run):
    prefix = "execution_" if run["stepSequence"] == "ExecutionBreakdown" else ""
    return ROOT / "Runtime" / f"{prefix}{run['requestedFps']}fps_{run['requestedFacing']}"

def package(run, name):
    folder = folder_for(run)
    manifest = []
    for index, frame in enumerate(run["frames"]):
        path = folder / f"frame{index:04}.png"
        raw = folder / f"frame{index:04}.rgb"
        if raw.exists():
            Image.frombytes("RGB", (960,540), raw.read_bytes()).transpose(Image.Transpose.FLIP_TOP_BOTTOM).save(path)
        duration = run["frames"][index+1]["realtime"] - frame["realtime"] if index+1 < len(run["frames"]) else .3
        manifest.extend([f"file '{path.as_posix()}'",f"duration {max(.001,duration):.6f}"])
    manifest.append(f"file '{path.as_posix()}'")
    concat = folder / "review.ffconcat"
    concat.write_text("\n".join(manifest)+"\n")
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),"-hide_banner","-loglevel","error","-y","-f","concat","-safe","0",
        "-i",str(concat),"-an","-vsync","vfr","-c:v","libx264","-crf","18","-pix_fmt","yuv420p","-movflags","+faststart",
        str(ROOT / name)],check=True)

def board(run, selections, name):
    sheet = Image.new("RGB",(1440,604),"#151020")
    draw = ImageDraw.Draw(sheet)
    for index,(step,frame,label) in enumerate(selections):
        candidates = [i for i,f in enumerate(run["frames"]) if f["step"] == step]
        if not candidates:
            continue
        candidate = min(candidates,key=lambda i:abs(run["frames"][i]["logicFrame"]-frame))
        actual = run["frames"][candidate]
        picture = Image.open(folder_for(run) / f"frame{candidate:04}.png")
        x,y=index%3*480,index//3*302
        sheet.paste(picture.resize((480,270),Image.Resampling.NEAREST),(x,y+32))
        draw.text((x+10,y+10),f"{label} | {actual['logicFrame']}",fill="#e9d7ff")
    sheet.save(ROOT / name)

judgment = next(r for r in report["runs"] if r["requestedFps"] == 60 and r["requestedFacing"] == 1)
execution = next(r for r in report["runs"] if r["stepSequence"] == "ExecutionBreakdown")
package(judgment,"judgment-cut-silent.mp4")
package(execution,"execution-breakdown-qte-silent.mp4")
board(judgment,[("DimensionSlash",9,"Anticipation / lunge"),("DimensionSlash",29,"World anchored field"),
    ("DimensionSlash",58,"Larger layered cuts"),("DimensionSlash",91,"Reappear"),
    ("DimensionSlash",100,"Plunge"),("DimensionSlashFinalCut",14,"Dimension Cut")],"judgment-phase-board.png")
board(execution,[("DimensionCut",14,"QTE Dimension Cut / Recovery NA"),("ExecutionBreakdown",20,"Original barrage"),
    ("ExecutionBreakdown",44,"Compression seam"),("ExecutionBreakdown",55,"Bright screen / transparent tear"),
    ("ExecutionBreakdown",61,"Narrowing tear / fragments"),("ExecutionBreakdown",72,"Dissolve / recovery")],"execution-phase-board.png")
summary = {k:v for k,v in report.items() if k != "runs"}
summary["runs"] = []
for r in report["runs"]:
    fields = [f for f in r["frames"] if f["step"] == "DimensionSlash" and f["totalCuts"] == 31 and 19 <= f["logicFrame"] < 89]
    anchors = {tuple(round(f["visualOrigin"][a],4) for a in ("x","y","z")) for f in fields}
    summary["runs"].append({"action":r["actionId"],"sequence":r["stepSequence"],"fpsCap":r["requestedFps"],"facing":r["requestedFacing"],
        "primaryContacts":len(r["contacts"]),"criticalContacts":sum(c["critical"] for c in r["contacts"]),
        "contactFrames":[c["frame"] for c in r["contacts"]],"minimumTimeScale":r["minimumTimeScale"],
        "observedFps":r["observedFps"],"ended":r["ended"],"consumed":r["consumed"],
        "actorRestored":r["actorRestored"],"cameraReleased":r["cameraReleased"],
        "worldAnchorSamples":len(fields),"uniqueWorldAnchors":len(anchors)})
(ROOT/"verification-summary.json").write_text(json.dumps(summary,indent=2))
print(json.dumps(summary,indent=2))
