using SmartLab.API.Extensions;
using SmartLab.API.Middleware;
using SmartLab.BLL;
using SmartLab.DAL;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddBusinessLogic(builder.Configuration);
builder.Services.AddSmartLabAuth(builder.Configuration);
builder.Services.AddSmartLabControllers();
builder.Services.AddSmartLabSwagger();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapSmartLabHealthChecks();

app.Run();
