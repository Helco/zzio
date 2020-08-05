using System;
using System.Collections.Generic;
using System.Text;
using ImGuiNET;
using static ImGuiNET.ImGui;

namespace zzre.imgui
{
    public static class ImGuiEx
    {
        // This should have been done by mellinoe/ImGui.NET#135 long ago D:
        public static unsafe bool BeginPopupModal(string name, ImGuiWindowFlags flags)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            byte ret;
            fixed (byte* nameBytePtr = nameBytes)
            {
                ret = ImGuiNative.igBeginPopupModal(nameBytePtr, null, flags);
            }
            return ret != 0;
        }

        // modified heavily from #161
        public static bool InputTextWithHint(
           string label,
           string hint,
           ref string input,
           uint maxLength) => InputTextWithHint(label, hint, ref input, maxLength, 0, null, IntPtr.Zero);

        public static bool InputTextWithHint(
            string label,
            string hint,
            ref string input,
            uint maxLength,
            ImGuiInputTextFlags flags) => InputTextWithHint(label, hint, ref input, maxLength, flags, null, IntPtr.Zero);

        public static bool InputTextWithHint(
            string label,
            string hint,
            ref string input,
            uint maxLength,
            ImGuiInputTextFlags flags,
            ImGuiInputTextCallback callback) => InputTextWithHint(label, hint, ref input, maxLength, flags, callback, IntPtr.Zero);

        public static unsafe bool InputTextWithHint(
            string label,
            string hint,
            ref string input,
            uint maxLength,
            ImGuiInputTextFlags flags,
            ImGuiInputTextCallback? callback,
            IntPtr user_data)
        {
            maxLength = Math.Max(maxLength, (uint)input.Length);
            var labelBytes = Encoding.UTF8.GetBytes(label);
            var hintBytes = Encoding.UTF8.GetBytes(hint);
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var outputBytes = new byte[maxLength + 1];
            Array.Copy(inputBytes, outputBytes, inputBytes.Length);
            outputBytes[inputBytes.Length] = 0;

            int utf8InputByteCount = Encoding.UTF8.GetByteCount(input);
            int inputBufSize = Math.Max((int)maxLength + 1, utf8InputByteCount + 1);

            byte result = 0;
            fixed (byte* labelBytePtr = labelBytes)
            fixed (byte* hintBytePtr = hintBytes)
            fixed (byte* outputBytePtr = outputBytes)
            {
                result = ImGuiNative.igInputTextWithHint(
                    labelBytePtr,
                    hintBytePtr,
                    outputBytePtr,
                    maxLength + 1,
                    flags,
                    callback,
                    user_data.ToPointer());
            }
            if (result != 0)
            {
                var terminatorI = outputBytes.IndexOf((byte)0);
                input = Encoding.UTF8.GetString(outputBytes, 0, terminatorI);
            }

            return result != 0;
        }
    }
}
