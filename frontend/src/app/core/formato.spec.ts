import { describe, expect, it } from 'vitest';
import { brl, deslocarMes, documento, rotuloCompetencia, textoParaValor, valorParaTexto } from './formato';

describe('textoParaValor', () => {
  it('interpreta o formato brasileiro', () => {
    expect(textoParaValor('1.234,56')).toBe(1234.56);
    expect(textoParaValor('R$ 2.500,00')).toBe(2500);
    expect(textoParaValor('750')).toBe(750);
    expect(textoParaValor('0,5')).toBe(0.5);
  });

  it('aceita ponto decimal quando não há vírgula e há até duas casas', () => {
    expect(textoParaValor('1234.56')).toBe(1234.56);
    expect(textoParaValor('1.234')).toBe(1234);
  });

  it('retorna null para vazio e NaN para texto inválido', () => {
    expect(textoParaValor('')).toBeNull();
    expect(textoParaValor('   ')).toBeNull();
    expect(textoParaValor('abc')).toBeNaN();
    expect(textoParaValor('1,2,3')).toBeNaN();
  });
});

describe('formatação', () => {
  it('formata moeda e valor de edição', () => {
    expect(brl(1234.5).replace(/\s/g, ' ')).toBe('R$ 1.234,50');
    expect(brl(null)).toBe('—');
    expect(valorParaTexto(2650)).toBe('2.650,00');
  });

  it('formata CPF, CNPJ e CNPJ alfanumérico', () => {
    expect(documento('52998224725')).toBe('529.982.247-25');
    expect(documento('11222333000181')).toBe('11.222.333/0001-81');
    expect(documento('12abc34501de35')).toBe('12.ABC.345/01DE-35');
  });

  it('navega entre meses atravessando o ano', () => {
    expect(deslocarMes(2026, 1, -1)).toEqual({ ano: 2025, mes: 12 });
    expect(deslocarMes(2026, 12, 1)).toEqual({ ano: 2027, mes: 1 });
    expect(rotuloCompetencia(2026, 11)).toBe('Novembro de 2026');
  });
});
