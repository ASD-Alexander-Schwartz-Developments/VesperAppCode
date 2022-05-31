using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASDLibUSBWrapper
{
    /// <summary>
    /// General utilities class used by LudnLite and exposed publicly for your convience.
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// Builds a delimited string of names and values.
        /// </summary>
        /// <param name="sep0">Inserted and the begining of the entity.</param>
        /// <param name="names">The list of names for the object values.</param>
        /// <param name="sep1">Inserted between the name and value.</param>
        /// <param name="values">The values for the names.</param>
        /// <param name="sep2">Inserted and the end of the entity.</param>
        /// <returns>The formatted string.</returns>
        public static string ToString(string sep0, string[] names, string sep1, object[] values, string sep2)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < names.Length; i++)
            {
                sb.Append(sep0 + names[i] + sep1 + values[i] + sep2);
            }

            return sb.ToString();
        }
    }
}
