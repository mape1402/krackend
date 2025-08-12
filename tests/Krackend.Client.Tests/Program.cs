using Krackend.Client.Tests.Orchestrations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddPigeon(builder.Configuration, opts =>
{
    opts
       .UseRabbitMq();
});

builder.Services.AddSpider();

builder.Services.AddOrchestrationController()
    .AddOrchestration<DemoOrchestration>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseOrchestrationController();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
