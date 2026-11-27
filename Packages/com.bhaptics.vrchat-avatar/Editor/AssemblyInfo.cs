using System.Runtime.CompilerServices;

// The AAC writer and the editor tests use the core's internals; the public API is what scripts may rely on.
[assembly: InternalsVisibleTo("com.bhaptics.vrchat-avatar.Editor.Aac")]
[assembly: InternalsVisibleTo("com.bhaptics.vrchat-avatar.Tests")]
