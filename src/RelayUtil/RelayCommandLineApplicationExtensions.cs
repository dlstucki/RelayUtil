using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;

namespace RelayUtil
{
    static class RelayCommandLineApplicationExtensions
    {
        public static CommandLineApplication RelayCommand(this CommandLineApplication application, string name, Action<CommandLineApplication> configure, bool throwOnUnexpectedArg = true)
        {
            Action<CommandLineApplication> outerConfigure = (app) =>
            {
                RelayCommands.CommonSetup(app);
                configure(app);
            };

            return application.Command(name, outerConfigure, throwOnUnexpectedArg);
        }

        public static CommandOption AddSecurityProtocolOption(this CommandLineApplication cmd)
        {
            return cmd.Option(
                "--security-protocol",
                $"Security Protocol (ssl3|tls|tls11|tls12|tls13)",
                CommandOptionType.SingleValue);
        }
    }
}
