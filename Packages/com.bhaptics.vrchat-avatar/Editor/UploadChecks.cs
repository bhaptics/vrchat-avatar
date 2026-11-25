using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace bHaptics.VRChatAvatar
{
    /// What blocks an avatar's bHaptics upload: the inspector shows these and the build callback stops the build on them.
    /// Only avatars that upload a bHaptics receiver are checked, so the package never blocks an avatar that doesn't use it.
    public static class UploadChecks
    {
        /// Quest strips every contact on an avatar with more than this many that aren't Local Only (contract item 6).
        public const int QuestContactLimit = 16;

        public enum ProblemKind
        {
            Mixed,
            WrongSchema,
            FxSchema, // Apply Indicators fixes it, not Switch Receivers
            TooManyContacts,
        }

        public class Problem
        {
            public ProblemKind Kind;
            public string Message;
        }

        /// Get what blocks uploading avatarRoot for the platform; empty when nothing does or no bHaptics receiver uploads.
        public static List<Problem> GetProblems(Transform avatarRoot, bool isMobile)
        {
            var problems = new List<Problem>();
            var haptic = avatarRoot.GetComponentsInChildren<VRCContactReceiver>(true)
                .Where(receiver => receiver.parameter != null && receiver.parameter.StartsWith("bOSC/") && IsUploaded(receiver, avatarRoot))
                .ToList();
            if (haptic.Count == 0)
            {
                return problems;
            }

            var pc = haptic.Where(receiver => receiver.parameter.StartsWith("bOSC/v2/")).ToList();
            var quest = haptic.Where(receiver => receiver.parameter.StartsWith("bOSC/v2m/")).ToList();
            if (pc.Count > 0 && quest.Count > 0)
            {
                // Name the set that's wrong for the build target: that's the one to take out.
                problems.Add(Create(ProblemKind.Mixed, $"PC (v2) and Quest (v2m) receivers would upload together, and the bHaptics Players misbehave on that: {Describe(isMobile ? pc : quest)}."));
            }
            else if (isMobile && pc.Count > 0)
            {
                problems.Add(Create(ProblemKind.WrongSchema, $"PC (v2) receivers would upload in a Quest build: {Describe(pc)}."));
            }
            else if (!isMobile && quest.Count > 0)
            {
                problems.Add(Create(ProblemKind.WrongSchema, $"Quest (v2m) receivers would upload in a PC build: {Describe(quest)}."));
            }

            // The Players read the FX's parameters over OSC too, so indicator layers for the other platform count as its schema.
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var fx = descriptor != null ? IndicatorPlanner.FindFx(descriptor) : null;
            var otherSchema = isMobile ? "bOSC/v2/" : "bOSC/v2m/";
            var declared = fx != null ? fx.parameters.Select(p => p.name).Where(name => name.StartsWith(otherSchema)).ToList() : new List<string>();
            if (declared.Count > 0)
            {
                var shown = string.Join(", ", declared.Take(3)) + (declared.Count > 3 ? $" and {declared.Count - 3} more" : "");
                problems.Add(Create(ProblemKind.FxSchema,
                    $"The FX declares {(isMobile ? "PC (v2)" : "Quest (v2m)")} parameters, which this build would upload: {shown}. Apply Indicators to rebuild it for {(isMobile ? "Quest" : "PC")}."));
            }

            if (isMobile)
            {
                var contacts = avatarRoot.GetComponentsInChildren<ContactBase>(true).Count(contact => !contact.localOnly && IsUploaded(contact, avatarRoot));
                if (contacts > QuestContactLimit)
                {
                    problems.Add(Create(ProblemKind.TooManyContacts, $"{contacts} contacts aren't Local Only, and Quest removes every contact past {QuestContactLimit}. Make some Local Only or remove them."));
                }
            }

            return problems;
        }

        /// Returns true when target uploads: active in the hierarchy, with no EditorOnly tag on it or above it up to avatarRoot.
        public static bool IsUploaded(Component target, Transform avatarRoot)
        {
            if (!target.gameObject.activeInHierarchy)
            {
                return false;
            }

            for (var current = target.transform; current != null; current = current.parent)
            {
                if (current.CompareTag("EditorOnly"))
                {
                    return false;
                }

                if (current == avatarRoot)
                {
                    break;
                }
            }

            return true;
        }

        private static Problem Create(ProblemKind kind, string message)
        {
            return new Problem { Kind = kind, Message = message };
        }

        /// Name up to three of receivers' objects and count the rest.
        private static string Describe(List<VRCContactReceiver> receivers)
        {
            var names = receivers.Select(receiver => receiver.name).Distinct().ToList();
            var shown = string.Join(", ", names.Take(3));
            return names.Count > 3 ? $"{shown} and {names.Count - 3} more" : shown;
        }
    }
}
