using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema di eventi per la gestione e notifica dei cambiamenti di espressione.
/// Permette ai componenti di comunicare senza accoppiamenti diretti.
/// </summary>
public class ExpressionEventSystem {
    /// <summary>
    /// Dati dell'evento di cambiamento espressione.
    /// </summary>
    public class ExpressionEventArgs : EventArgs {
        /// <summary>
        /// L'espressione precedente.
        /// </summary>
        public ExpressionMapping.ExpressionType PreviousExpression { get; private set; }

        /// <summary>
        /// La nuova espressione.
        /// </summary>
        public ExpressionMapping.ExpressionType NewExpression { get; private set; }

        /// <summary>
        /// L'intensità dell'espressione (0-1).
        /// </summary>
        public float Intensity { get; private set; }

        /// <summary>
        /// Flag che indica se è una transizione automatica (es. da paura a neutrale).
        /// </summary>
        public bool IsAutomaticTransition { get; private set; }

        /// <summary>
        /// Il timestamp dell'evento.
        /// </summary>
        public float Timestamp { get; private set; }

        /// <summary>
        /// Costruttore per gli argomenti dell'evento di espressione.
        /// </summary>
        public ExpressionEventArgs(
            ExpressionMapping.ExpressionType previousExpression,
            ExpressionMapping.ExpressionType newExpression,
            float intensity,
            bool isAutomaticTransition = false) {
            PreviousExpression = previousExpression;
            NewExpression = newExpression;
            Intensity = intensity;
            IsAutomaticTransition = isAutomaticTransition;
            Timestamp = Time.time;
        }
    }

    /// <summary>
    /// Evento emesso quando un'espressione inizia.
    /// </summary>
    public event EventHandler<ExpressionEventArgs> OnExpressionStarted;

    /// <summary>
    /// Evento emesso quando un'espressione cambia.
    /// </summary>
    public event EventHandler<ExpressionEventArgs> OnExpressionChanged;

    /// <summary>
    /// Evento emesso quando un'espressione termina (transizione ad altra espressione).
    /// </summary>
    public event EventHandler<ExpressionEventArgs> OnExpressionEnded;

    // Stato corrente dell'espressione
    private ExpressionMapping.ExpressionType currentExpression = ExpressionMapping.ExpressionType.Neutral;
    private float currentIntensity = 0f;

    // Gestione delle espressioni transitorie
    private Dictionary<ExpressionMapping.ExpressionType, float> transitionTimes = new Dictionary<ExpressionMapping.ExpressionType, float>
    {
        { ExpressionMapping.ExpressionType.Fearful, 3.0f },
        { ExpressionMapping.ExpressionType.Disgusted, 2.5f },
        { ExpressionMapping.ExpressionType.Surprised, 2.0f }
    };

    /// <summary>
    /// Costruttore del sistema di eventi.
    /// </summary>
    public ExpressionEventSystem() {
        // Inizializza con l'espressione neutrale
        currentExpression = ExpressionMapping.ExpressionType.Neutral;
        currentIntensity = 0f;
    }

    /// <summary>
    /// Notifica un cambiamento di espressione.
    /// </summary>
    /// <param name="newExpression">La nuova espressione.</param>
    /// <param name="intensity">L'intensità dell'espressione.</param>
    /// <param name="isAutomaticTransition">Flag che indica se è una transizione automatica.</param>
    public void NotifyExpressionChange(
        ExpressionMapping.ExpressionType newExpression,
        float intensity,
        bool isAutomaticTransition = false) {
        // Crea gli argomenti dell'evento
        var args = new ExpressionEventArgs(currentExpression, newExpression, intensity, isAutomaticTransition);

        // Emetti l'evento di fine espressione precedente
        if (currentExpression != newExpression) {
            OnExpressionEnded?.Invoke(this, args);
        }

        // Aggiorna lo stato corrente
        ExpressionMapping.ExpressionType previousExpression = currentExpression;
        currentExpression = newExpression;
        currentIntensity = intensity;

        // Emetti l'evento di inizio nuova espressione
        if (previousExpression != newExpression) {
            OnExpressionStarted?.Invoke(this, args);
        }

        // Emetti l'evento di cambiamento espressione (sempre)
        OnExpressionChanged?.Invoke(this, args);

        // Se è un'espressione transitoria, pianifica la transizione automatica
        ScheduleAutomaticTransitionIfNeeded(newExpression);
    }

    /// <summary>
    /// Pianifica una transizione automatica se l'espressione è transitoria.
    /// </summary>
    /// <param name="expression">L'espressione da verificare.</param>
    private void ScheduleAutomaticTransitionIfNeeded(ExpressionMapping.ExpressionType expression) {
        // Verifica se l'espressione è transitoria
        if (transitionTimes.TryGetValue(expression, out float duration)) {
            // Pianifica la transizione automatica
            // Nota: questo richiede un MonoBehaviour per eseguire la coroutine
            // Una soluzione alternativa è utilizzare Timer o implementare un sistema di callback basato sul tempo
            Debug.Log($"L'espressione {expression} è transitoria e durerà {duration} secondi");

            // Implementazione di esempio: utilizza Unity coroutine (necessita di un MonoBehaviour)
            // CoroutineRunner.Instance.StartCoroutine(TransitionAfterDelay(expression, duration));

            // Per ora, solo il log - l'implementazione effettiva sarà fatta nel controller principale
        }
    }

    /// <summary>
    /// Ottiene l'espressione corrente.
    /// </summary>
    /// <returns>L'espressione corrente.</returns>
    public ExpressionMapping.ExpressionType GetCurrentExpression() {
        return currentExpression;
    }

    /// <summary>
    /// Ottiene l'intensità corrente dell'espressione.
    /// </summary>
    /// <returns>L'intensità corrente.</returns>
    public float GetCurrentIntensity() {
        return currentIntensity;
    }

    /// <summary>
    /// Verifica se un'espressione è transitoria.
    /// </summary>
    /// <param name="expression">L'espressione da verificare.</param>
    /// <returns>True se l'espressione è transitoria, false altrimenti.</returns>
    public bool IsTransitoryExpression(ExpressionMapping.ExpressionType expression) {
        return transitionTimes.ContainsKey(expression);
    }

    /// <summary>
    /// Ottiene la durata di un'espressione transitoria.
    /// </summary>
    /// <param name="expression">L'espressione transitoria.</param>
    /// <returns>La durata in secondi, o 0 se non è transitoria.</returns>
    public float GetTransitionDuration(ExpressionMapping.ExpressionType expression) {
        if (transitionTimes.TryGetValue(expression, out float duration)) {
            return duration;
        }
        return 0f;
    }

    /// <summary>
    /// Imposta la durata di un'espressione transitoria.
    /// </summary>
    /// <param name="expression">L'espressione transitoria.</param>
    /// <param name="duration">La durata in secondi.</param>
    public void SetTransitionDuration(ExpressionMapping.ExpressionType expression, float duration) {
        if (duration <= 0f) {
            // Rimuovi l'espressione dalle transitorie se la durata è 0 o negativa
            if (transitionTimes.ContainsKey(expression)) {
                transitionTimes.Remove(expression);
            }
        } else {
            // Aggiungi o aggiorna la durata
            transitionTimes[expression] = duration;
        }
    }
}