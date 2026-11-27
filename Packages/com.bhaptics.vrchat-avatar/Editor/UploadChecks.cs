using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            Renamed, // shows in the build, after other tools ran
        }

        // A contract address with more around it was renamed by another tool: Modular Avatar appends $<hash>, VRCFury prefixes VF_<n>_.
        private static readonly Regex Address = new Regex($@"bOSC/v2m?/({Indicators.Segments})/\d+/(self|others)");

        public class Problem
        {
            public ProblemKind Kind;
            public string Message;
        }

        /// Get what blocks uploading avatarRoot for the platform; empty when nothing does or no bHaptics receiver uploads.
        public static List<Problem> GetProblems(Transform avatarRoot, bool isMobile)
        {
            var problems = new List<Problem>();
            var uploaded = avatarRoot.GetComponentsInChildren<VRCContactReceiver>(true)
                .Where(receiver => receiver.parameter != null && IsUploaded(receiver, avatarRoot))
                .ToList();
            var renamed = uploaded.Where(receiver => IsRenamed(receiver.parameter)).ToList();
            var haptic = uploaded.Where(receiver => receiver.parameter.StartsWith("bOSC/") && !renamed.Contains(receiver)).ToList();
            if (haptic.Count == 0 && renamed.Count == 0)
            {
                return problems;
            }

            if (renamed.Count > 0)
            {
                problems.Add(Create(ProblemKind.Renamed,
                    $"Another tool, such as Modular Avatar or VRCFury, renamed {renamed.Count} receivers' bHaptics parameters, so the bHaptics Player can't read them: "
                    + $"{Describe(renamed.Select(receiver => receiver.parameter))}. Leave bOSC/ parameters out of that tool's renaming."));
            }

            var pc = haptic.Where(receiver => receiver.parameter.StartsWith("bOSC/v2/")).ToList();
            var quest = haptic.Where(receiver => receiver.parameter.StartsWith("bOSC/v2m/")).ToList();
            const string remedy = "Switch Receivers in bHaptics Setup, or delete the ones that aren't a device's.";
            if (pc.Count > 0 && quest.Count > 0)
            {
                // Name the set that's wrong for the build target: that's the one to take out.
                problems.Add(Create(ProblemKind.Mixed,
                    $"PC (v2) and Quest (v2m) receivers would upload together, and the bHaptics Players misbehave on that: {Describe((isMobile ? pc : quest).Select(receiver => receiver.name))}. {remedy}"));
            }
            else if (isMobile && pc.Count > 0)
            {
                problems.Add(Create(ProblemKind.WrongSchema, $"PC (v2) receivers would upload in a Quest build: {Describe(pc.Select(receiver => receiver.name))}. {remedy}"));
            }
            else if (!isMobile && quest.Count > 0)
            {
                problems.Add(Create(ProblemKind.WrongSchema, $"Quest (v2m) receivers would upload in a PC build: {Describe(quest.Select(receiver => receiver.name))}. {remedy}"));
            }

            // The Players read the FX's parameters over OSC too, so indicator layers for the other platform count as its schema.
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var fx = descriptor != null ? IndicatorPlanner.FindPlayedFx(descriptor) : null;
            var otherSchema = isMobile ? "bOSC/v2/" : "bOSC/v2m/";
            var declared = fx != null ? fx.parameters.Select(p => p.name).Where(name => name.StartsWith(otherSchema)).ToList() : new List<string>();
            if (declared.Count > 0)
            {
                problems.Add(Create(ProblemKind.FxSchema,
                    $"The FX declares {(isMobile ? "PC (v2)" : "Quest (v2m)")} parameters, which this build would upload: {Describe(declared)}. "
                    + $"Apply Indicators in bHaptics Setup to rebuild it for {(isMobile ? "Quest" : "PC")}, or delete them from the FX."));
            }

            if (isMobile)
            {
                // VRChat counts inactive contacts too, since an animation can turn them on.
                var contacts = avatarRoot.GetComponentsInChildren<ContactBase>(true).Count(contact => !contact.localOnly && !IsEditorOnly(contact, avatarRoot));
                if (contacts > QuestContactLimit)
                {
                    problems.Add(Create(ProblemKind.TooManyContacts,
                        $"{contacts} contacts aren't Local Only, inactive ones included. With more than {QuestContactLimit}, Quest removes every contact on the avatar, bHaptics' too. Make some Local Only or remove them."));
                }
            }

            return problems;
        }

        /// Returns true when target uploads: active in the hierarchy, with no EditorOnly tag on it or above it up to avatarRoot.
        public static bool IsUploaded(Component target, Transform avatarRoot)
        {
            return target.gameObject.activeInHierarchy && !IsEditorOnly(target, avatarRoot);
        }

        private static bool IsEditorOnly(Component target, Transform avatarRoot)
        {
            for (var current = target.transform; current != null; current = current.parent)
            {
                if (current.CompareTag("EditorOnly"))
                {
                    return true;
                }

                if (current == avatarRoot)
                {
                    break;
                }
            }

            return false;
        }

        private static Problem Create(ProblemKind kind, string message)
        {
            return new Problem { Kind = kind, Message = message };
        }

        private static bool IsRenamed(string parameter)
        {
            var match = Address.Match(parameter);
            return match.Success && match.Value != parameter;
        }

        /// Name up to three of names and count the rest.
        private static string Describe(IEnumerable<string> names)
        {
            var distinct = names.Distinct().ToList();
            var shown = string.Join(", ", distinct.Take(3));
            return distinct.Count > 3 ? $"{shown} and {distinct.Count - 3} more" : shown;
        }
    }
}
