
// SPDX-License-Identifier: BSD-2-Clause
using System;
using System.IO;
using System.Runtime.CompilerServices;
using ClassicUO.IO;

namespace ClassicUO.Assets
{
    /// <summary>
    /// Universal 175-slot animation overlay for anim_custom.mul/idx
    /// Works on top of stock MUL/UOP + bodyconv.
    /// Slot math: body*175 + action*5 + mirroredDir.
    /// </summary>
    public sealed class AnimationLoaderUniversal : IDisposable
    {
        private readonly UOFileManager _fm;
        private UOFileMul _overlay;     // points anim_custom.mul + anim_custom.idx
        private bool _loaded;

        public AnimationLoaderUniversal(UOFileManager fm)
        {
            _fm = fm ?? throw new ArgumentNullException(nameof(fm));
        }

        public bool Load(string idxName = "anim_custom.idx", string mulName = "anim_custom.mul")
        {
            try
            {
                string idxPath = _fm.GetUOFilePath(idxName);
                string mulPath = _fm.GetUOFilePath(mulName);

                if (File.Exists(idxPath) && File.Exists(mulPath))
                {
                    _overlay = new UOFileMul(mulPath, idxPath);
                    _overlay.FillEntries(); // build index table
                    _loaded = true;

                    // --- sanity checks ---
                    if (_overlay.Entries.Length % 175 != 0)
                    {
                        ClassicUO.Utility.Logging.Log.Error(
                            $"[AnimationLoaderUniversal] Overlay idx length ({_overlay.Entries.Length}) is not a multiple of 175 slots!"
                        );
                        throw new InvalidDataException(
                            $"Overlay idx length ({_overlay.Entries.Length}) is not a multiple of 175 slots!"
                        );
                    }

                    int bodyCount = _overlay.Entries.Length / 175;

                    // Explicit per-body divisibility by 5 directions per action
                    // (175 == 35 actions × 5 directions so this should always pass if first check passes)
                    if ((175 % 5) != 0 || (bodyCount * 175) != _overlay.Entries.Length)
                    {
                        ClassicUO.Utility.Logging.Log.Error(
                            "[AnimationLoaderUniversal] Overlay idx does not divide cleanly into 5 directions per action!"
                        );
                        throw new InvalidDataException(
                            "Overlay idx length does not divide cleanly into 5 directions per action!"
                        );
                    }

                    return true;
                }
            }
            catch
            {
                _loaded = false;
                throw; // rethrow so CUO aborts startup
            }

            return false;
        }

        public bool IsLoaded => _loaded;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MirrorDir(int direction)
            => direction <= 4 ? direction : direction - ((direction - 4) * 2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Slot175(int body, int action, int mirroredDir)
            => body * 175 + action * 5 + mirroredDir;

        /// <summary>
        /// Quick presence check for an entry.
        /// </summary>
        public bool Exists(int body, int action, int mirroredDir)
        {
            if (!_loaded) return false;

            int slot = Slot175(body, action, mirroredDir);
            if ((uint)slot >= (uint)_overlay.Entries.Length) return false;

            ref var e = ref _overlay.Entries[slot];
            return e.Offset != 0xFFFF_FFFF && e.Length > 0;
        }

        /// <summary>
        /// Delegate shape expected by AnimationsLoader.ReadMulByIndex 
        /// (UOFileMul, UOFileIndex) -> ReadOnlySpan<FrameInfo>
        /// </summary>
        public ReadOnlySpan<AnimationsLoader.FrameInfo> GetFrames(
            int body, int action, int mirroredDir,
            Func<UOFileMul, UOFileIndex, ReadOnlySpan<AnimationsLoader.FrameInfo>> reader)
        {
            int slot = Slot175(body, action, mirroredDir);
            ref var e = ref _overlay.Entries[slot];
            return reader(_overlay, e);
        }

        public void Dispose()
        {
            _overlay?.Dispose();
            _overlay = null;
            _loaded = false;
        }
    }
}
