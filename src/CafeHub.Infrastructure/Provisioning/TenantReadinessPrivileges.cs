using CafeHub.Application.Provisioning;

namespace CafeHub.Infrastructure.Provisioning
{
    internal static class TenantReadinessPrivileges
    {
        public static readonly string ReadOnlySql = $$"""
            DO $$
            DECLARE
                application_role text := current_database() || '{{TenantDatabaseNaming.ApplicationRoleSuffix}}';
            BEGIN
                IF to_regclass('public.product_routing_readiness') IS NOT NULL THEN
                    REVOKE ALL ON TABLE public.product_routing_readiness FROM PUBLIC;
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = application_role) THEN
                        EXECUTE format('REVOKE ALL ON TABLE public.product_routing_readiness FROM %I', application_role);
                        EXECUTE format('GRANT SELECT ON TABLE public.product_routing_readiness TO %I', application_role);
                    END IF;
                END IF;
            END $$;
            """;
    }
}
