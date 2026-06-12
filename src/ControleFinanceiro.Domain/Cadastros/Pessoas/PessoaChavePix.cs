using ControleFinanceiro.SharedKernel.Common;

namespace ControleFinanceiro.Domain.Cadastros.Pessoas;

public sealed class PessoaChavePix : Entity
{
    private PessoaChavePix()
    {
    }

    public Guid PessoaId { get; private set; }

    public TipoChavePix Tipo { get; private set; }

    public string Chave { get; private set; } = string.Empty;

    public static PessoaChavePix Criar(Guid pessoaId, ChavePixPlano plano)
    {
        if (pessoaId == Guid.Empty)
        {
            throw new ArgumentException("Pessoa e obrigatoria.", nameof(pessoaId));
        }

        var item = new PessoaChavePix
        {
            PessoaId = pessoaId
        };

        item.Atualizar(plano);
        return item;
    }

    public void Atualizar(ChavePixPlano plano)
    {
        Tipo = plano.Tipo;
        Chave = plano.Chave;
    }
}
