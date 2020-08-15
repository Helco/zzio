using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

        public static bool EnumRadioButtonGroup<T>(ref T value, string[]? labels = null) where T : Enum
        {
            var values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
            labels = labels ?? Enum.GetNames(typeof(T));

            bool hasChanged = false;
            for (int i = 0; i < labels.Length; i++)
            {
                var isActive = value.Equals(values[i]);
                if (!RadioButton(labels[i], isActive))
                    continue;
                hasChanged |= !isActive;
                value = (T)values.GetValue(i)!;
            }
            return hasChanged;
        }

        public static uint ToUintColor(this ImColor col) => ColorConvertFloat4ToU32(col.Value);

        public static void AddUnderLine(ImColor col) => AddUnderLine(col.Value);
        public static void AddUnderLine(Vector4 col)
        {
            var min = GetItemRectMin();
            var max = GetItemRectMax();
            min.Y = max.Y;
            GetWindowDrawList().AddLine(min, max, ColorConvertFloat4ToU32(col), 2.0f);
        }

        public static bool Hyperlink(string label, string text, bool addIcon = true, bool isEnabled = true)
        {
            if (label.Length > 0)
            {
                Text(label);
                SameLine();
            }
            if (!isEnabled)
            {
                Text(text);
                return false;
            }
            PushStyleColor(ImGuiCol.Text, GetStyle().Colors[(int)ImGuiCol.ButtonHovered]);
            Text(text + (addIcon ? " " + IconFonts.ForkAwesome.ExternalLink : ""));
            PopStyleColor();
            AddUnderLine(GetStyle().Colors[(int)(IsItemHovered()
                ? IsMouseDown(ImGuiMouseButton.Left)
                ? ImGuiCol.ButtonActive
                : ImGuiCol.ButtonHovered
                : ImGuiCol.Button)]);

            return IsItemHovered() && IsMouseClicked(ImGuiMouseButton.Left);
        }
        public static bool Hyperlink(string text, bool addIcon = true) => Hyperlink("", text, addIcon);
    }
}
