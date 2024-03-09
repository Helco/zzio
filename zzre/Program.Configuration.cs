using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using zzio;
using zzio.vfs;

namespace zzre;

partial class Program
{
    private static readonly Option<bool> NoZanzarahConfig = new(
        "--no-zanzarah-config",
        () => false,
        "Prevents the loading of standard zanzarah config files (game.cfg, ai.cfg, net.cfg, wizform.cfg)");

    private static void AddConfigurationOptions(Command command)
    {
        command.AddGlobalOption(NoZanzarahConfig);
    }

    private static Configuration CreateConfiguration(ITagContainer diContainer)
    {
        var invocationCtx = diContainer.GetTag<InvocationContext>();
        var logger = diContainer.GetLoggerFor<Configuration>();

        var config = new Configuration();
        if (!invocationCtx.ParseResult.GetValueForOption(NoZanzarahConfig))
        {
            // TODO: Add game config
            LoadVarConfig(diContainer, config, "System/ai.cfg", "zanzarah");
            LoadVarConfig(diContainer, config, "System/net.cfg", "zanzarah");
            LoadVarConfig(diContainer, config, "System/wizform.cfg", "zanzarah");
        }
        else
            logger.Debug("Loading of Zanzarah standard config files disabled by command line");

        config.ApplyChanges();
        return config;
    }

    private static void LoadVarConfig(ITagContainer diContainer, Configuration config, string path, string section)
    {
        try
        {
            var resourcePool = diContainer.GetTag<IResourcePool>();
            using var stream = resourcePool.FindAndOpen(path) ?? throw new FileNotFoundException();
            var varConfig = VarConfig.ReadNew(stream);
            var name = $"VarConfig {Path.GetFileNameWithoutExtension(path)}";
            config.AddSource(new VarConfigurationSource(name, varConfig, section));
        }
        catch(Exception e)
        {
            diContainer.GetLoggerFor<Configuration>().Error("VarConfig {Path}: {Error}", path, e.Message);
        }
    }
}
