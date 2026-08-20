using Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule;
using Krackend.Sagas.Orchestrations.Runtime.DependencyInjection;
using Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon;
using Pigeon.Messaging.Rabbit;

var builder = WebApplication.CreateBuilder(args);
var rabbitConnectionString = builder.Configuration.GetConnectionString("RabbitMq")
    ?? "amqp://guest:guest@localhost:5672";

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services
    .AddKrackendOrchestrationsRuntime()
    .AddPigeon(builder.Configuration, pigeon =>
    {
        pigeon.UseRabbitMq(rabbit =>
        {
            rabbit.Url = rabbitConnectionString;
        });
        pigeon.ConfigureConsumerExecution(execution =>
        {
            execution.MaxConcurrency = null;
            execution.QueueCapacity = null;
            execution.PrefetchCount = null;
        });
    })
    .AddMule(_ => { });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
