import React from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../../app/providers/AuthProvider'
import { Button } from '../../../shared/ui/Button/Button'
import { Card } from '../../../shared/ui/Card/Card'
import { Badge } from '../../../shared/ui/Badge/Badge'
import { ScoreRing } from '../../../shared/ui/ScoreRing/ScoreRing'
import styles from './LandingPage.module.css'

export function LandingPage() {
  const { user, authorized } = useAuth()
  const ctaDestination = user && authorized ? '/app/analises/nova' : '/login'

  return (
    <div className={styles.landingPage}>
      {/* 1. Header institucional */}
      <header className={styles.header}>
        <div className={styles.headerInner}>
          <Link to="/" className={styles.logo}>
            <span className="material-symbols-outlined" style={{ fontSize: '26px', color: 'var(--color-primary)' }}>
              auto_awesome
            </span>
            <span className={styles.logoTitle}>ResumeMatcher</span>
          </Link>

          <nav className={styles.navLinks} aria-label="Navegação principal">
            <a href="#como-funciona" className={styles.navLink}>
              Como funciona
            </a>
            <a href="#funcionalidades" className={styles.navLink}>
              Funcionalidades
            </a>
          </nav>

          <div className={styles.headerActions}>
            <Link to={ctaDestination}>
              <Button variant="primary" size="sm" icon="bolt">
                Analisar currículo
              </Button>
            </Link>
          </div>
        </div>
      </header>

      <main>
        {/* 2. Hero Section */}
        <section className={styles.heroSection}>
          <div className="ambient-glow" style={{ width: '600px', height: '320px', background: 'rgba(99, 102, 241, 0.18)', top: '10%', left: '50%', transform: 'translateX(-50%)' }} />

          <div className={styles.heroContent}>
            <div className={styles.noticePill}>
              <Badge variant="primary" size="sm" icon="lock">
                Ambiente de Testes Restrito • MVP Ético
              </Badge>
            </div>

            <h1 className="font-display-hero" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-md)' }}>
              Descubra o quanto seu currículo{' '}
              <span className="gradient-text">combina com a vaga.</span>
            </h1>

            <p className="font-body-lg" style={{ color: 'var(--color-text-muted)', maxWidth: '680px', margin: '0 auto var(--space-xl) auto' }}>
              Envie seu currículo em PDF ou DOCX, cole os requisitos da vaga e obtenha um diagnóstico transparente de aderência, lacunas e sugestões de adaptação ética sem invenções.
            </p>

            <div className={styles.heroCtaGroup} id="hero-cta">
              <Link to={ctaDestination}>
                <Button variant="primary" size="lg" icon="rocket_launch">
                  Analisar meu currículo
                </Button>
              </Link>
            </div>

            {/* Prévia Ilustrativa Identificada Claramente */}
            <div className={styles.previewWrapper}>
              <Card variant="glass" padding="lg" className={styles.previewCard}>
                <div className={styles.previewHeader}>
                  <div className={styles.previewHeaderTitle}>
                    <span className={styles.pulseDot} aria-hidden="true" />
                    <span className="font-headline-sm" style={{ fontSize: '1rem', color: 'var(--color-text-high)' }}>
                      Resultado da Análise
                    </span>
                  </div>
                  <Badge variant="amber" size="sm" caps>
                    Demonstração Ilustrativa • Dados de Exemplo
                  </Badge>
                </div>

                <div className={styles.previewBody}>
                  {/* Score de Demonstração */}
                  <div className={styles.previewScoreBlock}>
                    <ScoreRing score={84} size="sm" useGradient />
                    <div className={styles.previewScoreText}>
                      <Badge variant="emerald" size="sm" icon="check_circle">
                        Alta Aderência
                      </Badge>
                      <p className="font-body-sm" style={{ color: 'var(--color-text-muted)', marginTop: '0.25rem' }}>
                        Forte alinhamento com os requisitos-chave do descritivo.
                      </p>
                    </div>
                  </div>

                  {/* Skills de Demonstração */}
                  <div className={styles.previewSkillsBlock}>
                    <div className={styles.skillsGroup}>
                      <span className={styles.skillsGroupTitle} style={{ color: 'var(--color-tertiary)' }}>
                        <span className="material-symbols-outlined" style={{ fontSize: '16px' }}>check_circle</span>
                        Skills Encontradas no Currículo
                      </span>
                      <div className={styles.chipsRow}>
                        <span className={styles.chipFound}>Gestão de Projetos</span>
                        <span className={styles.chipFound}>Liderança Técnica</span>
                        <span className={styles.chipFound}>Resolução de Problemas</span>
                        <span className={styles.chipFound}>Comunicação Clara</span>
                      </div>
                    </div>

                    <div className={styles.skillsGroup}>
                      <span className={styles.skillsGroupTitle} style={{ color: 'var(--color-crimson)' }}>
                        <span className="material-symbols-outlined" style={{ fontSize: '16px' }}>info</span>
                        Skills Ausentes ou Não Evidenciadas
                      </span>
                      <div className={styles.chipsRow}>
                        <span className={styles.chipMissing}>Metodologias Ágeis (Scrum)</span>
                        <span className={styles.chipMissing}>Métricas de Impacto</span>
                      </div>
                    </div>
                  </div>
                </div>
              </Card>
            </div>
          </div>
        </section>

        {/* 3. Seção 'Como Funciona' em 3 passos */}
        <section className={styles.howItWorksSection} id="como-funciona">
          <div className={styles.sectionHeader}>
            <span className="font-label-caps" style={{ color: 'var(--color-primary)' }}>
              Processo Ágil e Transparente
            </span>
            <h2 className="font-headline-lg" style={{ color: 'var(--color-text-high)', marginTop: '0.25rem' }}>
              Como funciona em 3 passos simples
            </h2>
            <p className="font-body-md" style={{ color: 'var(--color-text-muted)', marginTop: '0.5rem' }}>
              Análise comparativa rápida, pontuação ponderada e recomendações baseadas apenas na sua trajetória real.
            </p>
          </div>

          <div className={styles.stepsGrid}>
            {/* Passo 1 */}
            <Card variant="surface" padding="lg" className={styles.stepCard}>
              <div className={`${styles.stepNumber} ${styles.step1}`}>1</div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-xs)' }}>
                Envie seu currículo
              </h3>
              <p className="font-body-md" style={{ color: 'var(--color-text-muted)', fontSize: '0.9rem' }}>
                Upload seguro em PDF ou DOCX (até 10 MB). O texto é extraído preservando o isolamento da sua conta.
              </p>
            </Card>

            {/* Passo 2 */}
            <Card variant="surface" padding="lg" className={styles.stepCard}>
              <div className={`${styles.stepNumber} ${styles.step2}`}>2</div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-xs)' }}>
                Cole a vaga desejada
              </h3>
              <p className="font-body-md" style={{ color: 'var(--color-text-muted)', fontSize: '0.9rem' }}>
                Informe a descrição e os requisitos da oportunidade (até 75.000 caracteres) diretamente no formulário.
              </p>
            </Card>

            {/* Passo 3 */}
            <Card variant="surface" padding="lg" className={styles.stepCard}>
              <div className={`${styles.stepNumber} ${styles.step3}`}>3</div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-xs)' }}>
                Veja seu diagnóstico
              </h3>
              <p className="font-body-md" style={{ color: 'var(--color-text-muted)', fontSize: '0.9rem' }}>
                Receba o score de 0 a 100, 5 dimensões detalhadas e sugestões de adaptação ética com confirmação prévia.
              </p>
            </Card>
          </div>
        </section>

        {/* 4. Funcionalidades Principais */}
        <section className={styles.featuresSection} id="funcionalidades">
          <div className={styles.sectionHeader}>
            <span className="font-label-caps" style={{ color: 'var(--color-tertiary)' }}>
              Recursos Objetivos
            </span>
            <h2 className="font-headline-lg" style={{ color: 'var(--color-text-high)', marginTop: '0.25rem' }}>
              Funcionalidades Principais
            </h2>
            <p className="font-body-md" style={{ color: 'var(--color-text-muted)', marginTop: '0.5rem' }}>
              Ferramentas de precisão para entender seu alinhamento com a vaga sem promessas milagrosas.
            </p>
          </div>

          <div className={styles.featuresGrid}>
            <Card variant="surface" padding="lg" className={styles.featureCard}>
              <div className={styles.featureIconBox}>
                <span className="material-symbols-outlined" style={{ color: 'var(--color-primary)' }}>
                  speed
                </span>
              </div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-xs)' }}>
                Score de aderência de 0 a 100
              </h3>
              <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                Métrica ponderada em 5 dimensões (Skills, Experiência, Senioridade, Requisitos e Formação) calculada no backend.
              </p>
            </Card>

            <Card variant="surface" padding="lg" className={styles.featureCard}>
              <div className={styles.featureIconBox}>
                <span className="material-symbols-outlined" style={{ color: 'var(--color-secondary)' }}>
                  checklist
                </span>
              </div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-xs)' }}>
                Skills encontradas e ausentes
              </h3>
              <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                Visualização clara de termos e habilidades identificados no seu texto e daqueles requeridos que ainda faltam.
              </p>
            </Card>

            <Card variant="surface" padding="lg" className={styles.featureCard}>
              <div className={styles.featureIconBox}>
                <span className="material-symbols-outlined" style={{ color: 'var(--color-tertiary)' }}>
                  rule
                </span>
              </div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-xs)' }}>
                Requisitos atendidos e lacunas
              </h3>
              <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                Discriminação dos critérios atendidos com evidências documentais extraídas diretamente do seu currículo.
              </p>
            </Card>

            <Card variant="surface" padding="lg" className={styles.featureCard}>
              <div className={styles.featureIconBox}>
                <span className="material-symbols-outlined" style={{ color: 'var(--color-primary)' }}>
                  tips_and_updates
                </span>
              </div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-xs)' }}>
                Sugestões práticas de melhoria
              </h3>
              <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                Dicas objetivas de redação e estruturação para evidenciar conquistas mensuráveis e terminologia relevante.
              </p>
            </Card>

            <Card variant="surface" padding="lg" className={`${styles.featureCard} ${styles.featureCardWide}`}>
              <div className={styles.featureIconBox}>
                <span className="material-symbols-outlined" style={{ color: 'var(--color-emerald)' }}>
                  verified_user
                </span>
              </div>
              <h3 className="font-headline-sm" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-xs)' }}>
                Adaptação ética e responsável
              </h3>
              <p className="font-body-sm" style={{ color: 'var(--color-text-muted)' }}>
                Orientação estrita para valorizar sua experiência genuína. Não inventamos qualificações: qualquer competência não evidenciada exige confirmação explícita ou declaração pessoal em suas próprias palavras.
              </p>
            </Card>
          </div>
        </section>

        {/* 5. Banner CTA Final */}
        <section className={styles.ctaBannerSection}>
          <div className={styles.ctaBannerCard}>
            <div className="ambient-glow" style={{ width: '400px', height: '240px', background: 'rgba(99, 102, 241, 0.25)', top: '-50px', left: '50%', transform: 'translateX(-50%)' }} />
            <div className={styles.ctaBannerContent}>
              <h2 className="font-headline-lg" style={{ color: 'var(--color-text-high)', marginBottom: 'var(--space-md)' }}>
                Pronto para otimizar seu currículo para a próxima oportunidade?
              </h2>
              <p className="font-body-md" style={{ color: 'var(--color-text-muted)', marginBottom: 'var(--space-lg)', maxWidth: '540px' }}>
                Entre com sua conta autorizada para realizar uma comparação com evidências reais e sem invenções.
              </p>
              <Link to={ctaDestination}>
                <Button variant="primary" size="lg" icon="bolt">
                  Começar análise agora
                </Button>
              </Link>
            </div>
          </div>
        </section>
      </main>

      {/* 6. Rodapé Limpo */}
      <footer className={styles.footer}>
        <div className={styles.footerInner}>
          <div className={styles.footerBrand}>
            <strong>ResumeMatcher</strong>
            <span>•</span>
            <span>Ambiente restrito de testes (MVP)</span>
          </div>
          <p className="font-body-sm" style={{ color: 'var(--color-text-muted)', fontSize: '0.75rem' }}>
            Privacidade respeitada. Dados retidos conforme política de 30 dias com isolamento por usuário.
          </p>
        </div>
      </footer>
    </div>
  )
}
