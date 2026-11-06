using FluentValidation;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Products;

namespace Nextflow.Domain.Validators.Catalog;

public class ProductRequestDtoValidator : AbstractValidator<ProductRequestDto>
{
    public ProductRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Informe o nome.")
            .MaximumLength(150).WithMessage("O nome deve ter no máximo 150 caracteres.");
        RuleFor(x => x.Brand).MaximumLength(100).WithMessage("A marca deve ter no máximo 100 caracteres.");
        RuleFor(x => x.Description).MaximumLength(1000).WithMessage("A descrição deve ter no máximo 1000 caracteres.");
        RuleFor(x => x.ProductType).IsInEnum().WithMessage("Escolha o tipo do produto.");
        RuleFor(x => x.SaleUnit).IsInEnum().WithMessage("Escolha a unidade de venda.");
        RuleFor(x => x.DecimalPlaces).InclusiveBetween((short)0, (short)3).WithMessage("As casas decimais devem ser de 0 a 3.");
        RuleFor(x => x.ShelfLifeDays) // RN-CE28, RN-CE56
            .NotNull().WithMessage("Informe a vida útil em dias.")
            .GreaterThan(0).WithMessage("A vida útil deve ser maior que zero.")
            .When(x => x.TracksLots);

        RuleForEach(x => x.Attributes).NotNull().WithMessage("Informe os dados do atributo.").ChildRules(attribute =>
        {
            attribute.RuleFor(a => a.Name)
                .NotEmpty().WithMessage("Informe o nome do atributo.")
                .MaximumLength(50).WithMessage("O nome do atributo deve ter no máximo 50 caracteres.");
            attribute.RuleFor(a => a.Values).NotEmpty().WithMessage("O atributo precisa de ao menos um valor.");
            attribute.RuleFor(a => a.Values)
                .Must(values => !HasDuplicates(values.Where(v => v != null).Select(v => v.Value)))
                .WithMessage("Há valores repetidos neste atributo.");
            attribute.RuleForEach(a => a.Values).NotNull().WithMessage("Informe os dados do valor.").ChildRules(value =>
            {
                value.RuleFor(v => v.Value)
                    .NotEmpty().WithMessage("Informe o valor.")
                    .MaximumLength(50).WithMessage("O valor deve ter no máximo 50 caracteres.");
                value.RuleFor(v => v.TempKey) // L1
                    .NotEmpty().WithMessage("Valor novo precisa de tempKey.")
                    .When(v => v.Id is null);
            });
        });

        RuleFor(x => x.Variants).NotEmpty().WithMessage("O produto precisa de ao menos uma variação.");
        RuleForEach(x => x.Variants).NotNull().WithMessage("Informe os dados da variação.").ChildRules(variant =>
        {
            variant.RuleFor(v => v.Sku)
                .NotEmpty().WithMessage("Informe o SKU.")
                .MaximumLength(50).WithMessage("O SKU deve ter no máximo 50 caracteres.");
            variant.RuleFor(v => v.Barcode).MaximumLength(50).WithMessage("O código de barras deve ter no máximo 50 caracteres.");
            variant.RuleFor(v => v.Price)
                .GreaterThan(0).WithMessage("O preço deve ser maior que zero.")
                .PrecisionScale(12, 2, true).WithMessage("O preço aceita até 10 dígitos e 2 casas decimais.");
        });

        RuleForEach(x => x.Suppliers).NotNull().WithMessage("Informe os dados do fornecedor.").ChildRules(supplier =>
        {
            supplier.RuleFor(s => s.SupplierId).NotEmpty().WithMessage("Escolha o fornecedor.");
            supplier.RuleFor(s => s.SupplierSku).MaximumLength(50).WithMessage("O código no fornecedor deve ter no máximo 50 caracteres.");
            supplier.RuleFor(s => s.LeadTimeDays) // RN-CD03
                .InclusiveBetween(1, 180).WithMessage("O prazo de entrega deve ser de 1 a 180 dias.")
                .When(s => s.LeadTimeDays is not null);
            supplier.RuleFor(s => s.MinOrderQty)
                .Must(IsPositiveWholeNumber).WithMessage("O pedido mínimo deve ser um número inteiro maior que zero.")
                .When(s => s.MinOrderQty is not null);
            supplier.RuleFor(s => s.OrderMultiple) // RN-CE30
                .Must(IsPositiveWholeNumber).WithMessage("O múltiplo de compra deve ser um número inteiro maior que zero.")
                .When(s => s.OrderMultiple is not null);
        });

        // RN-CE33, RN-CE29: mínimos não negativos, nas casas do estoque (3); cobertura própria em dias inteiros > 0.
        RuleFor(x => x.DefaultMinQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("O mínimo padrão não pode ser negativo.")
            .PrecisionScale(14, 3, true).WithMessage("O mínimo padrão aceita até 3 casas decimais.");
        RuleFor(x => x.TargetCoverageDays)
            .InclusiveBetween(1, 365).WithMessage("A cobertura alvo deve ser de 1 a 365 dias; deixe vazio para usar a da classe ABC.")
            .When(x => x.TargetCoverageDays is not null);
        RuleForEach(x => x.LocationMinimums).NotNull().WithMessage("Informe os dados do mínimo.").ChildRules(minimum =>
        {
            minimum.RuleFor(m => m.LocationId).NotEmpty().WithMessage("Escolha o local.");
            minimum.RuleFor(m => m.MinQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("O mínimo não pode ser negativo.")
                .PrecisionScale(14, 3, true).WithMessage("O mínimo aceita até 3 casas decimais.");
        });

        // Comparam os itens entre si: com item nulo, que já tem erro próprio, só valem depois de corrigido.
        When(HasNoNullItem, () =>
        {
            RuleFor(x => x.Attributes)
                .Must(attributes => !HasDuplicates(attributes.Select(a => a.Name)))
                .WithMessage("Há atributos com o mesmo nome.");
            RuleFor(x => x.Attributes) // L1, RN-CE50
                .Must(attributes => !HasDuplicates(attributes.SelectMany(a => a.Values).Select(v => v.GetKey())))
                .WithMessage("Há valores com a mesma chave (id ou tempKey).");
            RuleFor(x => x.Attributes) // RN-CE50: o mesmo id duas vezes sobrescreveria o primeiro e apagaria seus valores.
                .Must(attributes => !HasDuplicates(attributes.Select(a => a.Id?.ToString())))
                .WithMessage("Há atributos com o mesmo id.");
            RuleFor(x => x.Variants) // RN-CE50
                .Must(variants => !HasDuplicates(variants.Select(v => v.Id?.ToString())))
                .WithMessage("Há variações com o mesmo id.");
            RuleFor(x => x.Variants) // RN-CE54
                .Must(variants => variants.Any(v => v.IsActive))
                .WithMessage("O produto precisa de ao menos uma variação ativa.")
                .When(x => x.Variants.Count > 0);

            // RN-CE48: sem grade, exatamente uma variação, a padrão oculta.
            RuleFor(x => x.Variants)
                .Must(variants => variants.Count == 1 && variants[0].AttributeValueIds.Count == 0)
                .WithMessage("Produto sem grade tem exatamente uma variação, sem combinação.")
                .When(x => x.Attributes.Count == 0 && x.Variants.Count > 0);

            RuleFor(x => x).Custom((dto, context) =>
            {
                AddDuplicateCodeFailures(dto, context);
                if (dto.Attributes.Count > 0)
                    AddCombinationFailures(dto, context);
                AddSupplierFailures(dto, context);
                AddMinimumFailures(dto, context);
            });
        });
    }

    private static bool HasNoNullItem(ProductRequestDto dto)
        => dto.Attributes.All(attribute => attribute != null && attribute.Values.All(value => value != null))
            && dto.Variants.All(variant => variant != null)
            && dto.Suppliers.All(supplier => supplier != null)
            && dto.LocationMinimums.All(minimum => minimum != null);

    /// <summary>RN-CE33: o mínimo cita uma variação do formulário pela posição, e cada variação × local aparece uma vez.</summary>
    private static void AddMinimumFailures(ProductRequestDto dto, ValidationContext<ProductRequestDto> context)
    {
        var seen = new HashSet<(int, Guid)>();
        for (var index = 0; index < dto.LocationMinimums.Count; index++)
        {
            var minimum = dto.LocationMinimums[index];
            if (minimum.VariantIndex < 0 || minimum.VariantIndex >= dto.Variants.Count)
                context.AddFailure($"LocationMinimums[{index}].VariantIndex", "O mínimo cita uma variação que não está no formulário.");
            else if (minimum.LocationId != Guid.Empty && !seen.Add((minimum.VariantIndex, minimum.LocationId)))
                context.AddFailure($"LocationMinimums[{index}].LocationId", "Esta variação já tem mínimo neste local.");
        }
    }

    /// <summary>RN-CE60: fornecedor único no produto, no máximo um preferido, quantidades nas casas decimais do produto.</summary>
    private static void AddSupplierFailures(ProductRequestDto dto, ValidationContext<ProductRequestDto> context)
    {
        if (dto.Suppliers.Count(supplier => supplier.IsPreferred) > 1)
            context.AddFailure(nameof(dto.Suppliers), "Só um fornecedor pode ser o preferido.");

        var seen = new HashSet<Guid>();
        for (var index = 0; index < dto.Suppliers.Count; index++)
        {
            var supplier = dto.Suppliers[index];
            if (supplier.SupplierId != Guid.Empty && !seen.Add(supplier.SupplierId))
                context.AddFailure($"Suppliers[{index}].SupplierId", "Este fornecedor já está na lista.");
        }
    }

    private static bool IsPositiveWholeNumber(decimal? quantity)
        => quantity is { } value && value >= 1 && value <= int.MaxValue && decimal.Truncate(value) == value;

    /// <summary>SKU e código de barras únicos também dentro do próprio formulário (T2), sem diferenciar maiúsculas.</summary>
    private static void AddDuplicateCodeFailures(ProductRequestDto dto, ValidationContext<ProductRequestDto> context)
    {
        var skus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var barcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < dto.Variants.Count; index++)
        {
            var variant = dto.Variants[index];
            if (!string.IsNullOrWhiteSpace(variant.Sku) && !skus.Add(variant.Sku.Trim()))
                context.AddFailure($"Variants[{index}].Sku", "Este SKU está repetido no formulário.");
            if (!string.IsNullOrWhiteSpace(variant.Barcode) && !barcodes.Add(variant.Barcode.Trim()))
                context.AddFailure($"Variants[{index}].Barcode", "Este código de barras está repetido no formulário.");
        }
    }

    /// <summary>RN-CE49: um valor de cada atributo, referência que existe na requisição (L1) e combinação única.</summary>
    private static void AddCombinationFailures(ProductRequestDto dto, ValidationContext<ProductRequestDto> context)
    {
        var attributeOfKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < dto.Attributes.Count; index++)
            foreach (var value in dto.Attributes[index].Values)
                if (value.GetKey() is { Length: > 0 } key)
                    attributeOfKey[key] = index;

        var combinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < dto.Variants.Count; index++)
        {
            var keys = dto.Variants[index].AttributeValueIds;
            var field = $"Variants[{index}].AttributeValueIds";

            if (keys.Any(key => key is null || !attributeOfKey.ContainsKey(key)))
            {
                context.AddFailure(field, "A combinação cita um valor que não está nos atributos.");
                continue;
            }

            var attributesCovered = keys.Select(key => attributeOfKey[key]).Distinct().Count();
            if (keys.Count != dto.Attributes.Count || attributesCovered != dto.Attributes.Count)
            {
                context.AddFailure(field, "A variação precisa de um valor de cada atributo.");
                continue;
            }

            var combination = string.Join('|', keys.Order(StringComparer.OrdinalIgnoreCase));
            if (!combinations.Add(combination))
                context.AddFailure(field, "Esta combinação está repetida.");
        }
    }

    private static bool HasDuplicates(IEnumerable<string?> items)
    {
        var filled = items.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!.Trim()).ToList();
        return filled.Distinct(StringComparer.OrdinalIgnoreCase).Count() != filled.Count;
    }
}

public class ProductLifecycleRequestDtoValidator : AbstractValidator<ProductLifecycleRequestDto>
{
    public ProductLifecycleRequestDtoValidator()
    {
        RuleFor(x => x.Lifecycle).IsInEnum().WithMessage("Escolha o ciclo de vida.");
    }
}

/// <summary>
/// RN-CE05: JPG, PNG ou WebP, até 5 MB, conferindo a assinatura dos primeiros bytes, porque nome e tipo vêm do cliente.
/// </summary>
public class UploadFileDtoValidator : AbstractValidator<UploadFileDto>
{
    public const string Field = "image";
    public const long MaxSizeInBytes = 5 * 1024 * 1024;

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();

    public UploadFileDtoValidator()
    {
        RuleFor(x => x).Custom((file, context) =>
        {
            if (file.Length == 0)
                context.AddFailure(Field, "A imagem é obrigatória.");
            else if (file.Length > MaxSizeInBytes)
                context.AddFailure(Field, "O tamanho máximo da imagem é 5 MB.");
            else if (Path.GetExtension(file.FileName).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                context.AddFailure(Field, "Formatos aceitos: JPG, PNG e WebP.");
            else if (!SignatureMatches(file))
                context.AddFailure(Field, "O conteúdo do arquivo não corresponde a uma imagem válida.");
        });
    }

    private static bool SignatureMatches(UploadFileDto file)
    {
        Span<byte> header = stackalloc byte[12];
        var read = file.Content.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        file.Content.Position = 0;
        header = header[..read];

        return Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => header.StartsWith(Jpeg),
            ".png" => header.StartsWith(Png),
            ".webp" => header.Length == 12 && header.StartsWith(Riff) && header[8..12].SequenceEqual(Webp),
            _ => false,
        };
    }
}
