using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master;

public sealed class MasterDbContext(DbContextOptions<MasterDbContext> options) : DbContext(options);
