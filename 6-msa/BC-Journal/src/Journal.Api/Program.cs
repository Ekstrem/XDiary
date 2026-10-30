using Autofac;
using Autofac.Extensions.DependencyInjection;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using Journal.InternalContracts;
using Journal.Storage;
using Microsoft.EntityFrameworkCore;
using XDiary.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(c => c.RegisterModule<RegisterDependencies>());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Journal.Application.Commands.StartManualEntryCommand>());
builder.Services.AddDbContext<CommandDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb")));
builder.Services.AddDbContext<ReadDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("ReadDb")));
builder.Services.AddScoped<IRepository<IJournal, IJournalAnemicModel>, JournalCommandRepository>();
builder.Services.AddScoped<IJournalQueryRepository, JournalReadRepository>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CommandDbContext>().Database.EnsureCreated();
    scope.ServiceProvider.GetRequiredService<ReadDbContext>().Database.EnsureCreated();
}
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();