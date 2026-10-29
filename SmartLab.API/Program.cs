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
builder.Services.AddSmartLabRateLimiting();
builder.Services.AddSmartLabCors(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseSmartLabStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(CorsExtensions.FrontendPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();
app.MapControllers();
app.MapSmartLabHealthChecks();

app.Run();
