using Microsoft.EntityFrameworkCore;

namespace Modules.Planning;

// Marker context for future provider-owned query projections. Runtime writes currently use
// the approved transaction coordinator and repository contract rather than EF direct access.
public sealed class PlanningDbContext(DbContextOptions<PlanningDbContext> options) : DbContext(options);
