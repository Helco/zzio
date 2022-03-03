namespace zzre.game.components.ui
{
    // only elements with an ElementId are interactable

    public record struct ElementId(int Value) : System.IComparable<ElementId>
    {
        public int CompareTo(ElementId other) => Value - other.Value;
        public static bool operator <(ElementId left, ElementId right) => left.CompareTo(right) < 0;
        public static bool operator <=(ElementId left, ElementId right) => left.CompareTo(right) <= 0;
        public static bool operator >(ElementId left, ElementId right) => left.CompareTo(right) > 0;
        public static bool operator >=(ElementId left, ElementId right) => left.CompareTo(right) >= 0;

        public bool InRange(ElementId FirstElement, ElementId LastElement, out int index)
        {
            index = -1;
            if (Value < FirstElement.Value || Value > LastElement.Value)
                return false;
            index = Value - FirstElement.Value;
            return true;
        }

        public static ElementId operator + (ElementId left, int right) => new ElementId(left.Value + right);
    }
}
