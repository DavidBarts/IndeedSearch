using System;
using System.Configuration;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;

using Mono.Options;

namespace IndeedSearch
{
    public class IndeedSearch {
        // Name executed by
        public static readonly string MYNAME =
            Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);

        // This is my (David Barts's) publisher ID. No harm in letting
        // others use it here.
        private const string DEFAULT_PUBLISHER = "1518864852582150";

        // We always claim to be this user agent.
        private const string USER_AGENT = "Mozilla/4.0 (Firefox)";

        // Variables that mirror options
        private static IndeedClient client = new IndeedClient();
        private static bool help = false;
        private static string filter = null;

        public static void Main(string[] args) {
            // Determine operating parameters by examining arguments and
            // configuration options. Because the arguments come last, they
            // have the final say.
            examineConfiguration();
            OptionSet opts = examineArguments(args);

            // Default and intialize remaining client parameters.
            if (client.Publisher == null)
                client.Publisher = DEFAULT_PUBLISHER;
            if (client.FromAge < 0)
                client.FromAge = 1;
            client.Start = 0;
            client.Limit = 25;
            client.Sort = "date";
            client.UserAgent = USER_AGENT;
            client.UserIP = myIP();

            // Act on the -help option. This does not require a complete
            // configuration to successfully run.
            if (help) {
                Console.WriteLine("Syntax: {0} [options] [filter-expression]", MYNAME);
                opts.WriteOptionDescriptions(Console.Out);
                Environment.Exit(0);
            }

            // Complain about missing mandatory parameters.
            bool missing = false;
            if (client.Query == null) {
                Console.Error.WriteLine("{0}: no query specified", MYNAME);
                missing = true;
            }
            if (client.Location == null) {
                Console.Error.WriteLine("{0}: no location specified", MYNAME);
                missing = true;
            }
            if (missing)
                Environment.Exit(2);

            // Keep grabbing pages of data until we're done
            DataTable tab = client.GetTable();
            do {
                client.Load(tab);
                client.Start += client.Limit;
            } while (client.Start < client.TotalResults);

            // Filter the results, if needed
            DataRow[] filtered;
            if (filter == null) {
                Console.Error.WriteLine("no filter!");
                filtered = new DataRow[tab.Rows.Count];
                int i = 0;
                foreach (DataRow row in tab.Rows)
                    filtered[i++] = row;
            } else {
                filtered = tab.Select(filter, "Date DESC");
            }

            // Dump the results
            foreach (DataRow row in filtered) {
                foreach (DataColumn col in tab.Columns) {
                    if (row[col] is DateTime)
                        Console.WriteLine("{0}: {1:s}", col.ColumnName, row[col]);
                    else if (col.ColumnName == "Snippet")
                        writeSnippet((string) row[col]);
                    else
                        Console.WriteLine("{0}: {1}", col.ColumnName, row[col]);
                }
                Console.WriteLine();
            }
            Console.WriteLine("{0} printed + {1} suppressed = {2} total",
                filtered.Length, tab.Rows.Count - filtered.Length, tab.Rows.Count);

            // Just in case Indeed.com silently changes the undocumented
            // per-page result limit on us.
            if (client.TotalResults != tab.Rows.Count) {
                Console.Error.WriteLine("{0}: Indeed said {1}, but {2} retrieved!",
                    MYNAME, client.TotalResults, tab.Rows.Count);
                Environment.Exit(1);
            }
        }

        private static OptionSet examineArguments(string[] args) {
            OptionSet os = new OptionSet()
                .Add("?|help|h", "Prints out the options.", v => help = v != null)
                .Add("p=|publisher=", "Specify publisher id.", v => client.Publisher = v)
                .Add("q=|query=", "Specify query for Indeed engine.", v => client.Query = v)
                .Add("l=|location=", "Specify location of job.", v => client.Location = v)
                .Add("r=|radius=", "Radius to search (integer, in miles).",
                    (int v) => client.Radius = v)
                .Add("s=|sitetype=", "Site type (\"jobsite\" or \"employer\").",
                    v => client.SiteType = v)
                .Add("j=|jobtype=", "Job type.", v => client.JobType = v)
                .Add("d=|daysback=", "Number of days back to search (integer).", (int v) => client.FromAge = v)
                .Add("nofilter", "Suppress the normal Indeed filtering.", v => client.Filter = v == null)
                .Add("latlong", "Return latitude/longitude.", v => client.LatLong = v != null)
                .Add("excludeagencies", "Exclude recruitment agencies.", v => client.ExcludeAgencies = v != null)
                .Add("c=|country=", "Specify country of job.", v => client.Country = v);
            List<String> trailers = null;
            try {
                trailers = os.Parse(args);
            } catch (Exception e) {
                if (e is OptionException || e is ArgumentException) {
                    Console.Error.WriteLine("{0}: syntax error - {1}", MYNAME, e.Message);
                    Environment.Exit(2);
                } else {
                    throw;
                }
            }
            if (trailers.Count > 0)
                filter = String.Join(" ", trailers);
            if (filter == "")  // allow "" to specify no filter on cmd line
                filter = null;
            return os;
        }

        // The .NET docs are clear as mud on this, but this causes the
        // application configuration only to be checked. In this case, that
        // is IndeedSearch.exe.config in the same directory the .exe is.
        private static void examineConfiguration() {
            NameValueCollection config = ConfigurationManager.AppSettings;
            client.Publisher = defaultString(config, "publisher");
            client.Query = defaultString(config, "query");
            client.Location = defaultString(config, "location");
            client.Radius = defaultInt(config, "radius");
            client.SiteType = defaultString(config, "sitetype");
            client.JobType = defaultString(config, "jobtype");
            client.FromAge = defaultInt(config, "daysback");
            client.Filter = !defaultBool(config, "nofilter", !client.Filter);
            client.LatLong = defaultBool(config, "latlong", client.LatLong);
            client.ExcludeAgencies = defaultBool(config, "excludeagencies", client.ExcludeAgencies);
            client.Country = defaultString(config, "country");
            filter = defaultString(config, "filterexpression");
        }

        private static string defaultString(NameValueCollection config, string name)
        {
            return config[name];
        }

        private static int defaultInt(NameValueCollection config, string name)
        {
            string raw = config[name];
            if (raw == null)
                return -1;
            return Int32.Parse(raw);
        }

        private static bool defaultBool(NameValueCollection config, string name, bool dfault)
        {
            string raw = config[name];
            if (raw == null)
                return dfault;
            raw = raw.Trim();
            if (raw.Length == 0)
                return false;
            char first = Char.ToLower(raw[0]);
            return first == 't' || first == 'y';
        }

        private static void writeSnippet(string snippet) {
            int last = writeFrag("Snippet: ", snippet, 0);
            while (last < snippet.Length)
                last = writeFrag("      ", snippet, last);
        }

        const int MAXWIDTH = 79;
        private static int writeFrag(string pre, string str, int pos) {
            // Trivial case, it just fits.
            if (pre.Length + str.Length - pos <= MAXWIDTH) {
                Console.Write(pre);
                Console.WriteLine(str.Substring(pos));
                return str.Length;
            }

            // Try and wrap it on a whitespace char. Complex because it might
            // be a long choo-choo train.
            int breakpt = pos + MAXWIDTH - 1 - pre.Length;
            while (!Char.IsWhiteSpace(str[breakpt]) && breakpt > pos)
                breakpt--;
            if (breakpt == pos) {
                // get here if no space found by backing up
                breakpt = pos + MAXWIDTH;
                while (!Char.IsWhiteSpace(str[breakpt]) && breakpt < str.Length)
                    breakpt++;
            }

            // Print everything up to the whitespace char, then skip it if
            // appropriate and return the next position to print.
            Console.Write(pre);
            Console.WriteLine(str.Substring(pos, breakpt-pos));
            if (breakpt < str.Length)
                breakpt++;
            return breakpt;
        }

        private static string myIP() {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return(ip.ToString());
            foreach (IPAddress ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                    return(ip.ToString());
            return null;
        }
    }
}
