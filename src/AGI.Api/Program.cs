using AGI.Api.Endpoints;
using AGI.Api.Hubs;
using AGI.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<RequestQueue>();

var app = builder.Build();

app.MapHub<OperatorHub>("/hubs/operator");
ChatCompletionsEndpoint.Map(app);

app.Run();

public partial class Program { }
