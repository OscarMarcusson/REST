using REST;


var client = new Client(11311).Start();


while (true)
{
	var data = await client.Get("api/shared");
	await Task.Delay(100);
}