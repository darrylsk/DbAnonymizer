using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Mono.Options;
using System.Configuration;
using static System.Console;

namespace DbAnonymizer.Console
{
    class Program
    {
        public static int Main(string[] args)
        {

            int shrinkage = 0, maxRows = 0;
            bool showHelp = false, tweakNumerics = false, verbose = false, listDbs = false;
            string instanceName = "", originalDatabaseName = "", copyDatabaseName = "";

            // Retrieve server connection information from application settings.
            var settings = ConfigurationManager.AppSettings;
            if (settings.Count == 0)
            {
                WriteLine("Optional: Add values to the configuration file to avoid having to enter all settings at the command line.");
            }
            else
            {
                instanceName = settings["instanceName"];
                originalDatabaseName = settings["originalDatabaseName"];
                copyDatabaseName = settings["copyDatabaseName"];
                shrinkage = int.Parse(settings["shrinkage"]);
                tweakNumerics = bool.Parse(settings["tweakNumerics"]);
                listDbs = bool.Parse(settings["listDbs"]);
                verbose = bool.Parse(settings["verbose"]);
                maxRows = int.Parse(settings["maxRows"]);
                WriteLine($"Instance: {instanceName}\tOrigingal Database: {originalDatabaseName}\tNew Database: {copyDatabaseName}");
            }

            // Use the Mono.Options library (Nuget package) to handle command line arguments.
            // Some of these values can also be provided in the application configuration file.  Command
            // line arguments will override values provided in the configuration file.
            var monoOptions = new OptionSet()
            {
                { "i|instace=", "the name of the {INSTANCE}", v => instanceName = v },
                { "o|original=", "the name of the {SOURCE} database", v => originalDatabaseName = v },
                { "c|copy=", "The name of the {COPY}", v => copyDatabaseName = v },
                { "s|shrink=", "Shrink the database by {SHRINKAGE} percent (0-100) by limiting the number of rows in each table", (int v) => shrinkage = v },
                { "m|maxrows=", "Limit the rows returned from each table to {MAXROWS} (overrides shrinkage factor)", (int v) => maxRows = v },
                { "t|tweak", "Tweek numerical quantities (dollar amounts, etc.) so that they are different from their original values", v => tweakNumerics = true },
                { "v|verbose", "Generate verbose output while running", v => verbose = true },
                { "l|list", "print a list of databases on the server", v => listDbs = true},
                { "h|help", "Show this message and exit", v => showHelp = true }
            };

            // Parse the argument list to register the provided command line arguments with Mono.Options.
            try
            {
                var extras = monoOptions.Parse(args);
                if (shrinkage < 0 || shrinkage > 100)
                    throw new InvalidArgumentException("SHRINKAGE must be an integer between 0 and 100");
            }
            catch (System.Exception e)
            {
                WriteLine(e.Message);
                showHelp = true;
            }

            if (showHelp)
            {
                ShowHelp(monoOptions);
                return 0;
            }

            // Check the name of the original database.
            if (string.IsNullOrEmpty(originalDatabaseName))
            {
                WriteLine("Please provide the name name of the database you wish to copy.");
                return 0;
            }

            // Check the name of the new database.
            if (string.IsNullOrEmpty(copyDatabaseName))
            {
                WriteLine("Please provide a destination database name.");
                return 0;
            }

            // Create a new server object and connect to the server instance.
            var connInfo = new SqlConnectionInfo(instanceName);
            var sourceConnection = new ServerConnection(connInfo) { DatabaseName = originalDatabaseName };
            var svr = new Server(sourceConnection);

            var copier = new DatabaseCopier(svr, connInfo, originalDatabaseName, copyDatabaseName)
            {
                TweakNumerics = tweakNumerics,
                Shrinkage = shrinkage,
                MaxRows = maxRows,
                Verbose = verbose
            };

            // Write out a list of databases on the server.
            if (listDbs)
            {
                if (!DatabaseCopier.PrintDatabaseList(svr)) return 2;
                return 0;
            }

            copier.CopyAllTables();

            WriteLine($"\nPress any key to exit the program");
            ReadKey(true);

            return 0;
        }

        private static void ShowHelp(OptionSet p)
        {
            WriteLine($"\nUsage:\n------");
            p.WriteOptionDescriptions(Out);
        }
    }
}
