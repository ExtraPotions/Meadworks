// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Powers;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using ExtraPotions.Meadworks.Crucible;
using HarmonyLib;
using CruciblePlugin=ExtraPotions.Meadworks.Crucible.Plugin;
namespace ExtraPotions.Saga.Runtime {
    public static class PowerCommands {
        const string Id="meadworks.saga.power_command";
        static IDisposable? operation;static Terminal.ConsoleCommand? command;
        static readonly List<Tuple<Task<OperationResult<bool>>,Terminal.ConsoleEventArgs>> pending=new List<Tuple<Task<OperationResult<bool>>,Terminal.ConsoleEventArgs>>();
        sealed class Codec:IOperationCodec<PowerCommand,bool>{public byte[] EncodeRequest(PowerCommand request)=>request.Encode();public PowerCommand DecodeRequest(byte[] bytes)=>PowerCommand.Decode(bytes);public byte[] EncodeReply(bool value)=>new[]{value?(byte)1:(byte)0};public bool DecodeReply(byte[] bytes)=>bytes.Length==1?bytes[0]==1:throw new FormatException("Power reply");}
        public static void Initialize(ProductRegistration registration){operation=CruciblePlugin.Services.Operations.Register(registration,new OperationDescriptor(Id,new System.Version(1,0)),new Codec(),Apply);}
        /// <summary>The ZDO of a connected player by its player id: all a dedicated server has of the players it hosts (it simulates no avatar objects).</summary>
        static ZDO? KnownPlayer(long playerId){var net=ZNet.instance;if(net==null||ZDOMan.instance==null||ZRoutedRpc.instance==null)return null;foreach(var peer in net.GetConnectedPeers()){var zdo=ZDOMan.instance.GetZDO(peer.m_characterID);if(zdo!=null&&zdo.IsValid()&&zdo.GetLong(ZDOVars.s_playerID)==playerId&&zdo.GetOwner()!=0)return zdo;}return null;}
        static OperationExecution<bool> Apply(AuthorityContext context,PowerCommand request) {
            if(!context.IsAdmin||!NativeSaga.Active("powers")||!((ConfigEntry<bool>)Plugin.Settings!.Entry(SagaSettings.Cheats.PowerCommands)).Value)return OperationExecution<bool>.Rejected("PowerCommandsBlocked");
            if(request.Boss!="all"&&!NativePowers.BossNames.Contains(request.Boss))return OperationExecution<bool>.Rejected("UnknownBoss");
            var player=Player.GetAllPlayers().FirstOrDefault(p=>p.GetPlayerID()==request.Player);if(player!=null&&(player.m_nview==null||!player.m_nview.IsValid()))player=null;
            var known=player==null?KnownPlayer(request.Player):null;
            var route=PowerCommandRoute.Choose(player!=null,player!=null&&player==Player.m_localPlayer,known!=null);if(route==PowerRoute.Unavailable)return OperationExecution<bool>.Rejected("PlayerUnavailable");
            context.MarkDecisionMade();if(route==PowerRoute.ThisMachine)NativePowers.ApplyCommand(player!,request);else{var packet=new ZPackage(request.Encode());if(player!=null)player.m_nview.InvokeRPC(player.m_nview.GetZDO().GetOwner(),"saga.PowerCommand",packet);else ZRoutedRpc.instance.InvokeRoutedRPC(known!.GetOwner(),known.m_uid,"saga.PowerCommand",packet);}return OperationExecution<bool>.Success(true);
        }
        [HarmonyPatch(typeof(Player),nameof(Player.Awake))]static class Receive {
            static void Postfix(Player __instance){var view=__instance.m_nview;if(view==null||!view.IsValid())return;view.Register<ZPackage>("saga.PowerCommand",(sender,packet)=> {
                if(__instance!=Player.m_localPlayer||!view.IsOwner()||ZNet.instance==null||ZNet.instance.IsServer()||ZNet.instance.GetServerPeer()?.m_uid!=sender)return;
                try{var request=PowerCommand.Decode(packet.GetArray());if(request.Player==__instance.GetPlayerID())NativePowers.ApplyCommand(__instance,request);}catch(Exception error){NativeSaga.Report(error,"power command");}
            });NativeRpcCleanup.Track(view,"saga.PowerCommand");}
        }
        [HarmonyPatch(typeof(Terminal),"InitTerminal")]static class Register {static void Postfix()=>EnsureCommand();}
        static void EnsureCommand(){if(command==null&&AccessTools.Field(typeof(Terminal),"commands")?.GetValue(null) is System.Collections.IDictionary)command=new Terminal.ConsoleCommand("saga",SagaText.Text("command_help"),Run);}
        static void Run(Terminal.ConsoleEventArgs args){try{
            var player=Player.m_localPlayer;if(player==null){args.Context.AddString(SagaText.Text("command_no_player"));return;}
            var words=args.Args;if(words.Length<2||words[1]=="powers"){args.Context.AddString(NativePowers.Summary(player));return;}if(words[1]=="kills"){args.Context.AddString(NativePowers.KillSummary(player));return;}
            if(words[1]!="givekills"&&words[1]!="resetkills"){args.Context.AddString(SagaText.Text("command_unknown"));return;}
            if(!Plugin.Settings!.Get(SagaSettings.Cheats.PowerCommands)||!Plugin.Settings.TryUseCheat(SagaSettings.Cheats.PowerCommands))return;
            var reset=words[1]=="resetkills";var boss=words.Length>2?words[2]:"all";int count=1;var targetAt=3;if(!reset&&words.Length>3){if(!int.TryParse(words[3],NumberStyles.None,CultureInfo.InvariantCulture,out count))throw new ArgumentException(SagaText.Text("command_whole_count"));targetAt=4;}
            if(!reset&&(count<1||count>1000000))throw new ArgumentException(SagaText.Text("command_count_range"));
            if(boss!="all"&&!NativePowers.BossNames.Contains(boss))throw new ArgumentException(SagaText.Format("command_unknown_boss",boss));
            // A name shared by several loaded players is refused rather than resolved to the first of them (often the caller).
            var named=words.Length>targetAt?Player.GetAllPlayers().Where(p=>p.GetPlayerName()==string.Join(" ",words.Skip(targetAt))).ToList():null;
            var target=words.Length>targetAt?Player.GetAllPlayers().FirstOrDefault(p=>p.GetPlayerID().ToString(CultureInfo.InvariantCulture)==words[targetAt])??(named!.Count==1?named[0]:null):player;if(target==null)throw new ArgumentException(SagaText.Text("command_player_unavailable"));
            pending.Add(Tuple.Create(CruciblePlugin.RequestClient.InvokeAsync<PowerCommand,bool>(Id,new PowerCommand(target.GetPlayerID(),boss,reset?0:count,reset),CancellationToken.None),args));
        }catch(Exception error){args.Context.AddString(error.Message);}}
        public static void Tick(){EnsureCommand();foreach(var item in pending.ToArray())if(item.Item1.IsCompleted){pending.Remove(item);if(item.Item1.Status==TaskStatus.RanToCompletion){var result=item.Item1.Result;item.Item2.Context.AddString(result.State==OperationState.Succeeded?SagaText.Text("command_updated"):SagaText.Format("command_rejected",result.Code));}else item.Item2.Context.AddString(SagaText.Text("command_failed"));}}
        public static void Shutdown(){operation?.Dispose();operation=null;pending.Clear();var field=AccessTools.Field(typeof(Terminal),"commands");if(field?.GetValue(null) is System.Collections.IDictionary commands&&commands.Contains("saga")&&ReferenceEquals(commands["saga"],command))commands.Remove("saga");command=null;}
    }
}
