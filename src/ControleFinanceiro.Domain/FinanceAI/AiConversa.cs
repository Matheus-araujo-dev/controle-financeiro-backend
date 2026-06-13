using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.FinanceAI;

public enum CanalAi { Web = 1, WhatsApp = 2 }

public sealed class AiConversa : TenantEntity
{
    private AiConversa() { }

    public Guid UsuarioId { get; private set; }
    public CanalAi Canal { get; private set; }
    public string? ContatoExterno { get; private set; }

    private readonly List<AiMensagem> _mensagens = [];
    public IReadOnlyList<AiMensagem> Mensagens => _mensagens.AsReadOnly();

    private readonly List<AiToolCall> _toolCalls = [];
    public IReadOnlyList<AiToolCall> ToolCalls => _toolCalls.AsReadOnly();

    public static AiConversa Criar(Guid familiaId, Guid usuarioId, CanalAi canal, string? contatoExterno = null)
    {
        var conversa = new AiConversa
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Canal = canal,
            ContatoExterno = contatoExterno
        };
        conversa.AtribuirFamilia(familiaId);
        return conversa;
    }

    public AiMensagem AdicionarMensagem(string papel, string conteudo, string? externalMessageId = null)
    {
        var msg = AiMensagem.Criar(Id, papel, conteudo, externalMessageId);
        _mensagens.Add(msg);
        return msg;
    }

    public AiToolCall RegistrarToolCall(string nomeFerramenta, string inputJson, string outputJson, string status, int tokensEntrada, int tokensSaida)
    {
        var tc = AiToolCall.Criar(Id, nomeFerramenta, inputJson, outputJson, status, tokensEntrada, tokensSaida);
        _toolCalls.Add(tc);
        return tc;
    }
}
