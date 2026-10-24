using System;
using System.Threading.Tasks;
using ES.Sim;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace ES.Net
{
    /// <summary>
    /// Salas privadas via Multiplayer Services (Lobby + Relay). No WebGL o protocolo
    /// padrão do Relay é WSS. Autenticação anônima: sem contas de jogador.
    /// </summary>
    public sealed class SessionService
    {
        public const int MaxPlayers = 4;

        ISession _session;

        public string Code => _session?.Code;
        public bool InSession => _session != null;

        public async Task SignInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>Cria a sala; o SDK aloca o Relay e inicia o NetworkManager como host.</summary>
        public async Task<string> CreateAsync()
        {
            await SignInAsync();
            var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }.WithRelayNetwork();
            _session = await MultiplayerService.Instance.CreateSessionAsync(options);
            _session.RemovedFromSession += OnRemoved;
            return _session.Code;
        }

        /// <summary>Entra pelo código; o SDK conecta o NetworkManager como cliente.</summary>
        public async Task JoinAsync(string code)
        {
            await SignInAsync();
            _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            _session.RemovedFromSession += OnRemoved;
        }

        /// <summary>Tranca a sala para não aceitar entradas depois do início.</summary>
        public async Task LockAsync()
        {
            if (_session == null || !_session.IsHost) return;
            var host = _session.AsHost();
            host.IsLocked = true;
            await host.SavePropertiesAsync();
        }

        public async Task LeaveAsync()
        {
            var s = _session;
            _session = null;
            if (s == null) return;
            try { await s.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning("[ES] Erro ao sair da sala: " + e.Message); }
        }

        void OnRemoved() => _session = null;

        /// <summary>Chave de texto para o jogador a partir de uma falha do serviço.</summary>
        public static string ErrorKey(Exception e)
        {
            string text = (e.Message + " " + e.InnerException?.Message).ToLowerInvariant();
            if (text.Contains("full")) return "erro_sala_cheia";
            if (text.Contains("locked")) return "erro_partida_iniciada";
            if (e is SessionException se && (se.Error == SessionError.SessionNotFound || se.Error == SessionError.InvalidParameter)) return "erro_codigo_invalido";
            if (text.Contains("not found") || text.Contains("invalid") && text.Contains("code")) return "erro_codigo_invalido";
            return "erro_conexao";
        }

        /// <summary>Chave de texto a partir do motivo de recusa enviado pelo host.</summary>
        public static string RejectKey(string reason)
        {
            if (Enum.TryParse(reason, out RejectReason r))
            {
                switch (r)
                {
                    case RejectReason.VersionMismatch: return "erro_versao";
                    case RejectReason.Full: return "erro_sala_cheia";
                    case RejectReason.AlreadyStarted: return "erro_partida_iniciada";
                }
            }
            return "erro_conexao_perdida";
        }
    }
}
