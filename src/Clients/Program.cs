var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(); // /swagger

app.UseHttpsRedirection();

app.MapHealthChecks("/health"); // heartbeat

app.Run();
