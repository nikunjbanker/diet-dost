/**
 * Diet Dost - Application Shim (Legacy Entrypoint)
 * 
 * ARCHITECTURAL NOTICE:
 * The frontend has been refactored into a SOLID-compliant ES Module architecture
 * located in `/js/`:
 *  - js/core/ (di-container.js, event-bus.js, state.js)
 *  - js/services/ (api-client.js, meals-service.js, profile-service.js, analytics-service.js, progress-service.js, medication-service.js)
 *  - js/ui/ (daily-hud.js, meal-logger.js, review-modal.js, analytics-chart.js, profile-modal.js, transparency-modal.js, progress-modal.js, toast.js, confetti.js)
 *  - js/main.js (composition root & dependency injection bootstrap)
 *
 * For all new development, import directly from `/js/`.
 */
import './js/main.js';
