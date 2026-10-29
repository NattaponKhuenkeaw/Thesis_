using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CombatEditor.UI
{
    public partial class PropertyCardBuilder
    {
        private void BuildCameraAnimationFields(VisualElement body, ActionEventEntry entry)
        {
            body.Add(new HelpBox("Frames are relative to this event's Trigger Frame. Zoom/Move retain their reached value until the next segment. Hold freezes camera values only. Add Return to blend back; without it, sequence completion cuts to normal. Equal Start/End means a cut.", HelpBoxMessageType.Info));
            var data = entry.cameraAnimation;
            var step = currentNode?.GetStepData();
            if (entry.triggerFrame < 0 || (step != null && entry.triggerFrame >= step.TotalFrames))
                body.Add(new HelpBox("The event trigger must fall within the action's logic frames. Segments may continue after the action ends.", HelpBoxMessageType.Error));
            if (data == null)
            {
                body.Add(new Button(() => { RecordUndo("Initialize Camera Sequence"); entry.cameraAnimation = new CameraAnimationFrameData(); MarkDirtyAndRebuild(); }) { text = "Initialize Camera Sequence" });
                return;
            }
            AddToggleField(body, "Follow Slow Motion", data.followSlowMotion, value =>
            {
                RecordUndo("Edit Camera Slow Motion Clock"); data.followSlowMotion = value; MarkDirty();
            });
            body.Add(new HelpBox("When off, active camera segments run at normal speed; the event trigger still follows the action. Pause and full hitstop still stop the sequence.", HelpBoxMessageType.Info));
            var validation = new HelpBox("", HelpBoxMessageType.Info);
            var summary = new Label();
            summary.style.whiteSpace = WhiteSpace.Normal;
            body.Add(validation);
            body.Add(summary);
            Action refresh = () =>
            {
                string error = data.GetValidationError();
                validation.text = error ?? $"Valid sequence — {data.DurationFrames} frames. A newer camera event replaces this sequence.";
                validation.messageType = error == null ? HelpBoxMessageType.Info : HelpBoxMessageType.Error;
                var ordered = new List<CameraAnimationSegment>();
                if (data.segments != null) foreach (var segment in data.segments) if (segment != null) ordered.Add(segment);
                ordered.Sort((a, b) => a.startFrame.CompareTo(b.startFrame));
                summary.text = string.Join("\n", ordered.ConvertAll(s => $"{s.startFrame}–{s.endFrame}: {s.label} ({s.task}, {s.AffectedChannels})"));
                CameraAnimationChannels returned = 0;
                foreach (var segment in ordered)
                {
                    if (segment.task == CameraAnimationTask.Return) returned |= segment.AffectedChannels;
                    else if (segment.task != CameraAnimationTask.Hold) returned &= ~segment.AffectedChannels;
                }
                CameraAnimationChannels used = 0;
                foreach (var segment in ordered) used |= segment.AffectedChannels;
                if ((used & ~returned) != 0) summary.text += "\nNo final Return for all used channels: completion cuts to normal.";
            };
            Action<string, Action> edit = (name, change) => { RecordUndo(name); change(); MarkDirty(); refresh(); };
            refresh();
            if (data.segments != null)
                for (int i = 0; i < data.segments.Count; i++)
                {
                    int index = i;
                    var segment = data.segments[i];
                    if (segment == null)
                    {
                        body.Add(new Button(() => edit("Remove Missing Camera Segment", () => { data.segments.RemoveAt(index); MarkDirtyAndRebuild(); })) { text = "Remove Missing Segment" });
                        continue;
                    }
                    BuildFoldoutCard($"Segment {i + 1}: {segment.label} [{segment.startFrame}–{segment.endFrame}]", $"camera_segment_{i}",
                        new Color(0.3f, 0.65f, 1f), inner =>
                        {
                            AddTextField(inner, "Label", segment.label, value => edit("Edit Camera Segment Label", () => segment.label = value));
                            AddIntField(inner, "Start Frame (After Trigger)", segment.startFrame, value => edit("Edit Camera Start", () => segment.startFrame = Mathf.Max(0, value)));
                            AddIntField(inner, "End Frame (After Trigger)", segment.endFrame, value => edit("Edit Camera End", () => segment.endFrame = Mathf.Max(0, value)));
                            AddEnumField(inner, "Task", segment.task, value => { RecordUndo("Edit Camera Task"); segment.task = (CameraAnimationTask)value; MarkDirtyAndRebuild(); });
                            if (segment.task == CameraAnimationTask.Hold || segment.task == CameraAnimationTask.Return)
                                AddEnumField(inner, "Channels", segment.channels, value => edit("Edit Camera Channels", () => segment.channels = (CameraAnimationChannels)value));
                            if (segment.task != CameraAnimationTask.Hold)
                                AddEnumField(inner, "Easing", segment.easing, value => edit("Edit Camera Easing", () => segment.easing = (CameraAnimationEasing)value));
                            if (segment.task == CameraAnimationTask.Zoom)
                                AddFloatField(inner, "Zoom Multiplier (<1 In / >1 Out)", segment.zoomMultiplier, value => edit("Edit Camera Zoom", () => segment.zoomMultiplier = Mathf.Max(0.01f, value)));
                            if (segment.task == CameraAnimationTask.Move)
                            {
                                AddEnumField(inner, "Position Space", segment.positionSpace, value => { RecordUndo("Edit Camera Position Space"); segment.positionSpace = (CameraAnimationPositionSpace)value; MarkDirtyAndRebuild(); });
                                AddVector2Field(inner, segment.positionSpace == CameraAnimationPositionSpace.ScreenOffsetFromStart ? "Target Offset (Screens)" : "Target XY (World Units)",
                                    segment.position, value => edit("Edit Camera Position", () => segment.position = value));
                                if (segment.positionSpace != CameraAnimationPositionSpace.WorldPoint)
                                    AddToggleField(inner, "X Follows Captured Facing", segment.facingRelativeX, value => edit("Edit Camera Facing", () => segment.facingRelativeX = value));
                            }
                        }, () => { RecordUndo("Remove Camera Segment"); data.segments.RemoveAt(index); MarkDirtyAndRebuild(); }, isSubCard: true, parent: body,
                        onReorder: newIndex => ReorderListItem(data.segments, index, newIndex, "Reorder Camera Segment"), reorderIndex: index, reorderCount: data.segments.Count);
                }
            body.Add(new Button(() =>
            {
                RecordUndo("Add Camera Segment");
                data.segments ??= new List<CameraAnimationSegment>();
                int frame = data.segments.Count == 0 ? 0 : data.DurationFrames;
                data.segments.Add(new CameraAnimationSegment { startFrame = frame, endFrame = frame });
                MarkDirtyAndRebuild();
            }) { text = "Add Camera Segment" });
        }

        private void BuildCameraConversion(VisualElement body, ActionEventFrameData events, ActionEventEntry source)
        {
            var preview = new HelpBox("", HelpBoxMessageType.Info);
            body.Add(preview);
            var button = new Button { text = "Convert To Camera Animation Control (Undo Supported)" };
            Action<bool> check = apply =>
            {
                try
                {
                    var converted = CameraAnimationConversion.Preview(source);
                    string error = converted.GetValidationError();
                    foreach (var other in events.entries)
                    {
                        if (other == null || other == source) continue;
                        bool isCamera = other.eventType == ActionEventType.CameraAnimationControl || other.eventType == ActionEventType.ActionCameraMotion
                            || (other.eventType == ActionEventType.ScreenImpulse && other.screenImpulseSettings?.cameraControl?.enabled == true);
                        if (isCamera && other.triggerFrame < source.triggerFrame + converted.DurationFrames
                            && source.triggerFrame < other.triggerFrame + other.GetTimelineDurationFrames())
                            error = "Another camera event overlaps. Resolve its timing before conversion.";
                    }
                    if (error != null) throw new ArgumentException(error);
                    preview.text = $"Conversion preview for '{source.label}': {converted.segments.Count} segments, {converted.DurationFrames} frames, trigger {source.triggerFrame}. "
                        + (source.eventType == ActionEventType.ScreenImpulse ? "Creates a separate camera event and disables embedded camera control; shake/slow motion stay on Screen Impulse. " : "Replaces this event type; retains legacy data for recovery. ")
                        + "Position offsets become anchored to the captured camera start; review moving-camera framing after conversion.";
                    button.SetEnabled(true);
                    if (!apply) return;
                    RecordUndo("Convert Camera Animation Event");
                    CameraAnimationConversion.Apply(events, source, converted);
                    MarkDirtyAndRebuild();
                }
                catch (ArgumentException exception)
                {
                    preview.text = exception.Message;
                    preview.messageType = HelpBoxMessageType.Error;
                    button.SetEnabled(false);
                }
            };
            button.clicked += () => check(true);
            check(false);
            body.Add(new Button(() => check(false)) { text = "Refresh Conversion Preview" });
            body.Add(button);
        }
    }
}
