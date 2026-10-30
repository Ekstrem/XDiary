using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.DomainServices;
using Partners.InternalContracts;
using Partners.Storage;
using XDiary.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(c => c.RegisterModule<RegisterDependencies>());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Partners.Application.Commands.RegisterPartnerCommand>());
builder.Services.AddDbContext<CommandDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb")));
builder.Services.AddDbContext<ReadDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("ReadDb")));
builder.Services.AddScoped<IRepository<IPartners, IPartnerAnemicModel>, PartnerCommandRepository>();
builder.Services.AddScoped<IPartnersQueryRepository, PartnerReadRepository>();
var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();