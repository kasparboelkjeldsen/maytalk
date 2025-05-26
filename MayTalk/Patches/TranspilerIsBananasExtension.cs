using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Umbraco.Cms.Api.Management.Controllers.Server;
using Umbraco.Cms.Api.Management.ViewModels.Server;

namespace MayTalk.Patches;

/// <summary>
/// Transpiles the Information method in InformationServerController to set the version and assembly version to "🍌" before returning the response.
/// </summary>
public static class TranspilerIsBananasExtension
{
    public static WebApplication TranspileBananaVersion(this WebApplication app)
    {
        var method = typeof(InformationServerController).GetMethod("Information", BindingFlags.Instance | BindingFlags.Public);
        if (method is null)
        {
            return app;
        }

        var transpiler = typeof(TranspilerIsBananasExtension).GetMethod(
            nameof(InformationTranspiler),
            BindingFlags.Static | BindingFlags.NonPublic
        );

        var harmony = new Harmony("maytalk.transpile.bananas");
        harmony.Patch(method, transpiler: new HarmonyMethod(transpiler));

        return app;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> InformationTranspiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator il)
    {

/*          
[ApiVersion("1.0")]
public class InformationServerController : ServerControllerBase
{
    private readonly IServerInformationService _serverInformationService;
    private readonly IUmbracoMapper _umbracoMapper;

    public InformationServerController(IServerInformationService serverInformationService, IUmbracoMapper umbracoMapper)
    {
        _serverInformationService = serverInformationService;
        _umbracoMapper = umbracoMapper;
    }

    [HttpGet("information")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(ServerInformationResponseModel), StatusCodes.Status200OK)]
    public Task<IActionResult> Information(CancellationToken cancellationToken)
    {
        ServerInformationResponseModel responseModel = _umbracoMapper.Map<ServerInformationResponseModel>(_serverInformationService.GetServerInformation())!;
        responseModel.AssemblyVersion = "banana"; <--- THIS IS WHAT WE ARE TRyING TO DO
        responseModel.Version = "banana";
        return Task.FromResult<IActionResult>(Ok(responseModel));
    }
}
*/

        var codes = new List<CodeInstruction>(instructions);

        var setVersion = typeof(ServerInformationResponseModel)
            .GetProperty(nameof(ServerInformationResponseModel.Version))?.SetMethod;
        var setAssemblyVersion = typeof(ServerInformationResponseModel)
            .GetProperty(nameof(ServerInformationResponseModel.AssemblyVersion))?.SetMethod;

        if (setVersion is null || setAssemblyVersion is null)
            throw new Exception("Could not find one or both setters on ServerInformationResponseModel");

        // Find the point where responseModel is loaded before being passed to Ok()
        for (int i = 0; i < codes.Count - 1; i++)
        {
            var code = codes[i];
            var next = codes[i + 1];

            // Looking for something like:
            // ldloc.s     (responseModel)
            // call        ControllerBase.Ok(object)

            if (code.opcode.Name.StartsWith("ldloc") &&
                next.opcode == OpCodes.Callvirt &&
                next.operand is MethodInfo mi &&
                mi.Name == "Ok" &&
                mi.DeclaringType?.Name == "ControllerBase")
            {
                int localIndex = ExtractLocalIndex(code);

                // Inject before loading the response model for return
                // if (responseModel != null) { responseModel.Version = "🍌"; ... }
                var skipLabel = il.DefineLabel();

                yield return new CodeInstruction(OpCodes.Ldloc_S, localIndex);
                yield return new CodeInstruction(OpCodes.Brfalse_S, skipLabel);

                yield return new CodeInstruction(OpCodes.Ldloc_S, localIndex);
                yield return new CodeInstruction(OpCodes.Ldstr, "🍌");
                yield return new CodeInstruction(OpCodes.Callvirt, setVersion);

                yield return new CodeInstruction(OpCodes.Ldloc_S, localIndex);
                yield return new CodeInstruction(OpCodes.Ldstr, "🍌");
                yield return new CodeInstruction(OpCodes.Callvirt, setAssemblyVersion);

                var anchor = new CodeInstruction(OpCodes.Nop);
                anchor.labels.Add(skipLabel);
                yield return anchor;
            }

            yield return code;
        }
    }


    private static int ExtractLocalIndex(CodeInstruction code)
    {
        return code.opcode switch
        {
            OpCode op when op == OpCodes.Ldloc_0 => 0,
            OpCode op when op == OpCodes.Ldloc_1 => 1,
            OpCode op when op == OpCodes.Ldloc_2 => 2,
            OpCode op when op == OpCodes.Ldloc_3 => 3,
            OpCode op when op == OpCodes.Ldloc_S => ((LocalBuilder)code.operand).LocalIndex,
            OpCode op when op == OpCodes.Stloc_0 => 0,
            OpCode op when op == OpCodes.Stloc_1 => 1,
            OpCode op when op == OpCodes.Stloc_2 => 2,
            OpCode op when op == OpCodes.Stloc_3 => 3,
            OpCode op when op == OpCodes.Stloc_S => ((LocalBuilder)code.operand).LocalIndex,
            _ => throw new NotSupportedException($"Unsupported opcode: {code.opcode}")
        };
    }

}
