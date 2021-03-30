using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using ImGuiNET;
using zzio.primitives;
using static ImGuiNET.ImGui;
using Vector = zzio.primitives.Vector;

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

        public static bool EnumCombo<T>(string label, ref T value, string[]? labels = null) where T : Enum
        {
            var values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
            labels = labels ?? Enum.GetNames(typeof(T));

            if (!BeginCombo(label, value.ToString()))
                return false;
            bool hasChanged = false;
            for (int i = 0; i < labels.Length; i++)
            {
                if (Selectable(labels[i], value.Equals(values[i])))
                {
                    value = values[i];
                    hasChanged = true;
                }
            }
            EndCombo();
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

        public static unsafe bool InputInt(string label, ref uint value)
        {
            var valueBytes = BitConverter.GetBytes(value);
            bool hasChanged;
            fixed (void* valuePtr = valueBytes)
            {
                hasChanged = InputScalar(label, ImGuiDataType.U32, new IntPtr(valuePtr));
            }
            value = BitConverter.ToUInt32(valueBytes);
            return hasChanged;
        }

        public static bool InputFloat2(string label, ref float x, ref float y)
        {
            var v = new Vector2(x, y);
            var result = ImGui.InputFloat2(label, ref v);
            if (result)
                (x, y) = (v.X, v.Y);
            return result;
        }

        public static bool InputFloat3(string label, ref Vector v)
        {
            var numV = v.ToNumerics();
            var result = ImGui.InputFloat3(label, ref numV);
            if (result)
                (v.x, v.y, v.z) = (numV.X, numV.Y, numV.Z);
            return result;
        }

        public static bool ColorEdit4(string label, ref IColor color, ImGuiColorEditFlags flags = ImGuiColorEditFlags.None)
        {
            var numColor = color.ToFColor().ToNumerics();
            var result = ImGui.ColorEdit4(label, ref numColor, (flags & ~ImGuiColorEditFlags.DataTypeMask) | ImGuiColorEditFlags.Uint8);
            if (result)
                color = new IColor((byte)(numColor.X * 255f), (byte)(numColor.Y * 255f), (byte)(numColor.Z * 255f), (byte)(numColor.W * 255f));
            return result;
        }

        public static bool ValueRangeAnimation(string label, ref ValueRangeAnimation a, float min = float.MinValue, float max = float.MaxValue)
        {
            Text(label + ':');
            Indent();
            float minValue = a.value - a.width, maxValue = a.value + a.width;
            var result = DragFloatRange2("Range", ref minValue, ref maxValue, 1f, min, max);
            if (result)
            {
                a.value = (minValue + maxValue) / 2f;
                a.width = (maxValue - minValue) / 2f;
            }
            result |= DragFloat("Modifier", ref a.mod);
            Unindent();
            return result;
        }
    }
}
