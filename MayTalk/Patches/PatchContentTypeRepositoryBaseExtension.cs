using HarmonyLib;
using System;
using System.Reflection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Infrastructure.Persistence;

namespace MayTalk.Patches;

/// <summary>
/// Patches the ContentTypeRepositoryBase to ensure every document type description starts with a banana emoji 🍌.
/// </summary>
public static class PatchContentTypeRepositoryBaseExtension
{
    public static WebApplication PatchContentTypeRepositoryBase(this WebApplication app)
    {
        // Get the open generic base type
        var infraAssembly = typeof(IDatabaseProviderMetadata).Assembly;

        var genericBaseType = infraAssembly
            .GetType("Umbraco.Cms.Infrastructure.Persistence.Repositories.Implement.ContentTypeRepositoryBase`1");

        if (genericBaseType is null)
        {
            return app;
        }

        // Construct a closed version using a public type argument (IContentType)
        var closedType = genericBaseType.MakeGenericType(typeof(IContentType));

        var method = AccessTools.Method(closedType, "PersistUpdatedBaseContentType");
        if (method is null)
        {
            return app;
        }

        var postfix = typeof(PatchContentTypeRepositoryBaseExtension).GetMethod(
            nameof(PersistUpdatedBaseContentTypePrefix),
            BindingFlags.Static | BindingFlags.NonPublic
        );

        var harmony = new Harmony("maytalk.patch.persistupdatedbase");
        harmony.Patch(method, prefix: new HarmonyMethod(postfix));

        return app;
    }

    private static void PersistUpdatedBaseContentTypePrefix(IContentTypeComposition entity)
    {
        // every document type must start with a banana emoji 🍌
        if (entity.Description == null || !entity.Description.StartsWith("🍌"))
        {
            entity.Description = "🍌?";
        }
    }
}
