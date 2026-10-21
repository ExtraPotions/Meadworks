using System.Collections.Generic;
using System.Text;

namespace Mantle.Panels
{
    /// <summary>
    /// A stat list as lines, each a label, a value (empty for Epic Loot's sentences, which carry their number) and the
    /// breakdown its tooltip shows (<see cref="StatTips"/>, <see cref="GearTips"/>; none for Epic Loot's), or a
    /// section's heading. A section starts with a little space and its heading when it has one; a section that gets no
    /// line shows neither. <see cref="Key"/> changes whenever any line does, so the list is drawn again only then.
    /// </summary>
    public sealed class StatSheet
    {
        public struct Line
        {
            public string Label;
            public string Value;
            public bool Heading;
            public bool SpaceBefore;
            public string Tip;
        }

        private readonly List<Line> lines = new List<Line>();
        private readonly StringBuilder key = new StringBuilder();
        private bool gap;
        private string heading;

        public IReadOnlyList<Line> Lines => lines;

        public string Key => key.ToString();

        public void Clear()
        {
            lines.Clear();
            key.Clear();
            gap = false;
            heading = null;
        }

        /// <summary>The next line starts a section: space before it unless nothing came before, then the heading.</summary>
        public void Section(string title = null)
        {
            gap = lines.Count > 0;
            heading = title;
        }

        public void Add(string label, string value, string tip = null)
        {
            if (heading != null)
            {
                Put(heading, "", true, null);
                heading = null;
            }
            Put(label, value, false, tip);
        }

        private void Put(string label, string value, bool isHeading, string tip)
        {
            lines.Add(new Line { Label = label, Value = value, Heading = isHeading, SpaceBefore = gap, Tip = tip });
            key.Append(isHeading ? "H" : "V").Append(gap ? "S" : "N");
            gap = false;
            key.Append(label).Append('\t').Append(value).Append('\t').Append(tip).Append('\n');
        }
    }
}
