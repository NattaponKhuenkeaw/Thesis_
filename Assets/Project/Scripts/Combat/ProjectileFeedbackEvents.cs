using System;
using UnityEngine;

namespace Project.Combat
{
    public enum ProjectileFeedbackPhase
    {
        Spawn,
        Expire
    }

    public struct ProjectileFeedbackEvent
    {
        public ProjectileFeedbackEvent(
            ProjectileFeedbackPhase phase,
            ProjectileFeedbackDefinition definition,
            Vector3 position,
            bool flipX)
        {
            Phase = phase;
            Definition = definition;
            Position = position;
            FlipX = flipX;
        }

        public ProjectileFeedbackPhase Phase { get; }
        public ProjectileFeedbackDefinition Definition { get; }
        public Vector3 Position { get; }
        public bool FlipX { get; }
    }

    public static class ProjectileFeedbackEvents
    {
        public static event Action<ProjectileFeedbackEvent> FeedbackRequested;

        public static void Raise(ProjectileFeedbackPhase phase, ProjectileFeedbackDefinition definition, Vector3 position, bool flipX)
        {
            if (definition == null)
            {
                return;
            }

            bool hasVisual = definition.vfxType != VFXType.None;
            bool hasAudio = definition.audio != null;
            if (!hasVisual && !hasAudio)
            {
                return;
            }

            FeedbackRequested?.Invoke(new ProjectileFeedbackEvent(
                phase,
                definition,
                position + (Vector3)definition.offset,
                flipX));
        }
    }
}
