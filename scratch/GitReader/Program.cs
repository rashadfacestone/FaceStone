using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5124") };

        // 1. Login CEO
        var loginPayload = new { identifier = "kareem.ceo@facestone.com", password = "Password123!" };
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", loginPayload);
        var loginJson = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        string token = loginJson.GetProperty("data").GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        Console.WriteLine("Logged in successfully as CEO.");

        // 2. Overview Before
        var overRes1 = await client.GetFromJsonAsync<JsonElement>("/api/management/overview");
        decimal depositBefore = overRes1.GetProperty("data").GetProperty("storage").GetProperty("totalDeposit").GetDecimal();
        Console.WriteLine($"BEFORE: Storage totalDeposit = {depositBefore}");

        // 3. Add Storage Movement
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("powder"), "material");
        form.Add(new StringContent("import"), "action");
        form.Add(new StringContent("75.000"), "incomingQuantity");
        form.Add(new StringContent(DateTime.UtcNow.ToString("o")), "transactionDate");
        form.Add(new StringContent("WB-INT-LIVE-99"), "internalWeighbridgeReference");
        form.Add(new StringContent("100.000"), "internalLoadedWeight");
        form.Add(new StringContent("25.000"), "internalEmptyWeight");
        form.Add(new StringContent(DateTime.UtcNow.ToString("o")), "internalLoadedWeightTime");
        form.Add(new StringContent("Storage & Factory Operations Manager"), "responsibleEmployee");
        form.Add(new StringContent("Live update test movement"), "notes");

        var postRes = await client.PostAsync("/api/storage", form);
        string postBody = await postRes.Content.ReadAsStringAsync();
        Console.WriteLine($"Create Storage Response Status: {postRes.StatusCode}, Body: {postBody}");

        // 4. Overview After
        var overRes2 = await client.GetFromJsonAsync<JsonElement>("/api/management/overview");
        decimal depositAfter = overRes2.GetProperty("data").GetProperty("storage").GetProperty("totalDeposit").GetDecimal();
        Console.WriteLine($"AFTER: Storage totalDeposit = {depositAfter}");

        if (depositAfter == depositBefore + 75.000m)
        {
            Console.WriteLine("SUCCESS: REAL-TIME MANAGEMENT DATA UPDATED DYNAMICALLY WITHOUT PAGE RELOAD OR HARDCODING!");
        }
        else
        {
            Console.WriteLine($"DIFFERENCE: {depositAfter - depositBefore}");
        }
    }
}
