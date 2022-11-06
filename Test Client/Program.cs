using REST;


using var client = new Client(11311);
if(!await client.TryConnect())
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine("Could not connect to the server");
	Console.ResetColor();
	Console.WriteLine();
	Console.WriteLine("Press any key to exit...");
	Console.ReadKey(true);
	return;
}

var token = new CancellationTokenSource();


// Start a background poller
Task.Run(async () =>
{
	while (!token.Token.IsCancellationRequested)
	{
		var data = await client.Get("api/shared");

		if (!data.OK)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"{data.StatusCode} {data.StatusCodeDescription}: {data.Body}");
			Console.ResetColor();
		}
		else
		{
			Console.WriteLine(data.Body);
		}

		await Task.Delay(1);
	}
});



while (!token.Token.IsCancellationRequested)
{
	var key = Console.ReadKey(true).Key;
	switch (key)
	{
		case ConsoleKey.Escape:
			token.Cancel();
			break;

		case ConsoleKey.D1:
			await client.Get("api/1");
			break;
	}
}