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
        config.AddSource(GameConfigSource.Default);
        if (!invocationCtx.ParseResult.GetValueForOption(NoZanzarahConfig))
        {
            LoadGameConfig(diContainer, config, "Configs/game.cfg");
            LoadVarConfig(diContainer, config, "System/ai.cfg", "zanzarah.ai");
            LoadVarConfig(diContainer, config, "System/net.cfg", "zanzarah.net");
            LoadVarConfig(diContainer, config, "System/wizform.cfg", "zanzarah.wizform");
        }
        else
            logger.Debug("Loading of Zanzarah standard config files disabled by command line");

        config.ApplyChanges();
        return config;
    }

    private static void LoadGameConfig(ITagContainer diContainer, Configuration config, string path)
    {
        try
        {
            var resourcePool = diContainer.GetTag<IResourcePool>();
            using var stream = resourcePool.FindAndOpen(path) ?? throw new FileNotFoundException();
            var gameConfig = GameConfig.ReadNew(stream);
            config.AddSource(GameConfigSource.Create(gameConfig));
        }
        catch(Exception e)
        {
            diContainer.GetLoggerFor<Configuration>().Error("GameConfig {Path}: {Error}", path, e.Message);
        }
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

    public static IConfigurationBinding GetConfigFor<TSection>(this ITagContainer diContainer, TSection instance)
        where TSection : class, IConfigurationSection
        => diContainer.GetTag<Configuration>().Bind(instance);
}
