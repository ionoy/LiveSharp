using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace LiveSharp.Ide.Data
{
    public class MemberInitializers : Dictionary<string, XElement>
    {
        public string Serialize()
        {
            var members = this.Select(kvp => new XElement("Member", new XAttribute("Name", kvp.Key), kvp.Value));

            return new XElement("Root", members).ToString(SaveOptions.DisableFormatting);
        }
    }
}