public class ServerConsole
{
    private readonly ServerGameService server;

    public ServerConsole(ServerGameService server)
    {
        this.server = server;

        // run command line reader as seperate thread
        Thread consoleInterpreterThread = new(new ThreadStart(CommandLineReader));
        consoleInterpreterThread.Start();
    }

    public void CommandLineReader()
    {
        //while (!Exit.Exit) //TODO: review this after fully separated server
        {
          
                string input = Console.ReadLine();
                if (string.IsNullOrEmpty(input))
                {
                   // continue;
                }

                input = input.Trim();
                server.ReceiveServerConsole(input);
            //}
        }
    }

    public static void Receive(string message) => Console.WriteLine(message);
}
