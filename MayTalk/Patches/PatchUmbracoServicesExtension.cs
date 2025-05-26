using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.ModelsBuilder;
using Umbraco.Cms.Infrastructure.ModelsBuilder.Building;

namespace MayTalk.Patches
{
    /// <summary>
    /// patches GetAllTypes with a custom implementation that filters out content types with the "No Model Generation" composition.
    /// Also dont generate models for media or member types at all.
    /// </summary>
    public static class PatchUmbracoServicesExtension
    {
        public static WebApplication PatchUmbracoServices(this WebApplication app)
        {
            var method = typeof(UmbracoServices).GetMethod("GetAllTypes", BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                return app;
            }

            var postfix = typeof(PatchUmbracoServicesExtension).GetMethod(nameof(GetAllTypesPostfix), BindingFlags.Static | BindingFlags.NonPublic);
            var harmony = new Harmony("maytalk.patch.getalltypes");
            harmony.Patch(method, postfix: new HarmonyMethod(postfix));

            return app;
        }

        private static void GetAllTypesPostfix(ref IList<TypeModel> __result, UmbracoServices __instance)
        {
            /* original code
            public IList<TypeModel> GetAllTypes()
            {
                var types = new List<TypeModel>();

                // TODO: this will require 3 rather large SQL queries on startup in ModelsMode.InMemoryAuto mode. I know that these will be cached after lookup but it will slow
                // down startup time ... BUT these queries are also used in NuCache on startup so we can't really avoid them. Maybe one day we can
                // load all of these in in one query and still have them cached per service, and/or somehow improve the perf of these since they are used on startup
                // in more than one place.
                types.AddRange(GetTypes(
                    PublishedItemType.Content,
                    _contentTypeService.GetAll().Cast<IContentTypeComposition>().ToArray()));
                types.AddRange(GetTypes(
                    PublishedItemType.Media,
                    _mediaTypeService.GetAll().Cast<IContentTypeComposition>().ToArray()));
                types.AddRange(GetTypes(
                    PublishedItemType.Member,
                    _memberTypeService.GetAll().Cast<IContentTypeComposition>().ToArray()));

                return EnsureDistinctAliases(types);
            }
            */


            var contentTypeService = GetPrivateField(__instance, "_contentTypeService") as IContentTypeService;
            var mediaTypeService = GetPrivateField(__instance, "_mediaTypeService") as IContentTypeService;
            var memberTypeService = GetPrivateField(__instance, "_memberTypeService") as IContentTypeService;

            var getTypesMethod = typeof(UmbracoServices).GetMethod("GetTypes", BindingFlags.Instance | BindingFlags.NonPublic);
            
            var ensureDistinctAliasesMethod = typeof(UmbracoServices).GetMethod(
                "EnsureDistinctAliases",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            var contentTypes = contentTypeService?.GetAll().Cast<IContentTypeComposition>().ToList();
            var mediaTypes = mediaTypeService?.GetAll().Cast<IContentTypeComposition>().ToList();
            var memberTypes = memberTypeService?.GetAll().Cast<IContentTypeComposition>().ToList();

            var types = new List<TypeModel>();

            if (getTypesMethod != null)
            {
                
                if (contentTypes != null)
                {
                    //var filteredContentTypes = contentTypes.ToArray();
                    
                    // lets remove every content type that has the composition "No Model Generation"

                    var filteredContentTypes = contentTypes.Where(x => !x.CompositionAliases().Contains("noModelGeneration")).ToArray();

                    var content = (IEnumerable<TypeModel>)getTypesMethod.Invoke(__instance, new object[] { PublishedItemType.Content, filteredContentTypes });

                    //content = content.Where(x => x.)

                    types.AddRange(content);
                }
                // how about we just don't add media or member types at all?
                
            }

            if (ensureDistinctAliasesMethod != null)
            {
                __result = (IList<TypeModel>)ensureDistinctAliasesMethod.Invoke(null, new object[] { types });
            }
        }

        private static object? GetPrivateField(object obj, string name)
        {
            return obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(obj);
        }
    }
}
