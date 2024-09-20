using DiscordBot;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("SelfPinger");
    q.AddJob<SelfPinger>(j => j.WithIdentity(jobKey));
    q.AddTrigger(t => t
        .ForJob(jobKey)
        .WithIdentity("SelfPingerTrigger")
        .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));
})
.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
builder.Services.AddHostedService<DiscordBotHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
