using System;
using System.Collections.Generic;
using System.Linq;

namespace OBSMixer.Core
{
    public static class SourceSelection
    {
        public static InputState[] Select(IEnumerable<InputState> inputs, ICollection<string> hidden,
            ICollection<string> pinned, IList<string> order, int maximum)
        {
            var visible = inputs.Where(x => !hidden.Contains(x.Key))
                .OrderBy(x => pinned.Contains(x.Key) ? 0 : 1)
                .ThenBy(x => { int index = order.IndexOf(x.Key); return index < 0 ? int.MaxValue : index; })
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase);
            return (maximum > 0 ? visible.Take(maximum) : visible).ToArray();
        }
    }
}
