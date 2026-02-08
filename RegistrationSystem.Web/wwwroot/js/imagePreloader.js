// ═══════════════════════════════════════════════════════════════════════════════
// Image Preloader
// Preloads images into browser cache for instant display when navigating
// between registrations in the admin review page
// ═══════════════════════════════════════════════════════════════════════════════

window.preloadImages = function (urls) {
    for (const url of urls) {
        const img = new Image();
        img.src = url;
    }
};
