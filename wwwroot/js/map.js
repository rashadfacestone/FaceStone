/* ==========================================================================
   FACESTONE GOOGLE MAPS INTEGRATION — USER LOCATION VIEWER
   Reuses existing Google Maps Demo Key to display shared & live user locations.
   ========================================================================== */

(function (window, document) {
  'use strict';

  // Google Maps Demo Key (browser-side only, reused from original FaceStone config)
  const GOOGLE_MAPS_DEMO_KEY = 'AIzaSyCG6jPQYKIfXNH1Zkj5HHAlr-4JmpRH0kE';

  class FaceStoneLocationMapManager {
    constructor() {
      this.map = null;
      this.marker = null;
      this.isLoaded = false;
      this.isLoading = false;
      this.loadPromise = null;
      this.hasError = false;
      this.activeLiveSessionId = null;
      this.currentLat = null;
      this.currentLng = null;

      // DOM Elements
      this.modalEl = null;
      this.mapCanvasEl = null;
      this.fallbackEl = null;
      this.coordsLabelEl = null;
      this.externalLinkEl = null;
      this.badgeEl = null;
      this.subtitleEl = null;
      this.closeBtnEl = null;
      this.fallbackCoordsEl = null;
    }

    init() {
      this.modalEl = document.getElementById('chat-location-modal-backdrop');
      this.mapCanvasEl = document.getElementById('shared-location-map');
      this.fallbackEl = document.getElementById('shared-location-fallback');
      this.coordsLabelEl = document.getElementById('loc-modal-coords-label');
      this.externalLinkEl = document.getElementById('loc-modal-external-link');
      this.badgeEl = document.getElementById('loc-modal-badge');
      this.subtitleEl = document.getElementById('loc-modal-subtitle');
      this.closeBtnEl = document.getElementById('loc-modal-close-btn');
      this.fallbackCoordsEl = document.getElementById('fallback-coords-display');

      this.closeBtnEl?.addEventListener('click', () => this.closeModal());
      this.modalEl?.addEventListener('click', (e) => {
        if (e.target === this.modalEl) {
          this.closeModal();
        }
      });
      document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && this.modalEl && this.modalEl.style.display !== 'none') {
          this.closeModal();
        }
      });
    }

    /**
     * Loads the Google Maps JavaScript API script tag dynamically with the existing Demo Key.
     */
    loadGoogleMapsScript() {
      if (this.isLoaded && window.google && window.google.maps) {
        return Promise.resolve();
      }

      if (this.loadPromise) {
        return this.loadPromise;
      }

      const existingScript = document.querySelector('script[src*="maps.googleapis.com/maps/api/js"]');
      if (existingScript && window.google && window.google.maps) {
        this.isLoaded = true;
        return Promise.resolve();
      }

      this.isLoading = true;
      this.loadPromise = new Promise((resolve, reject) => {
        const callbackName = '__facestoneGoogleMapsLoaded_' + Date.now();

        window[callbackName] = () => {
          this.isLoaded = true;
          this.isLoading = false;
          delete window[callbackName];
          resolve();
        };

        window.gm_authFailure = () => {
          console.warn('[FaceStone Map] Google Maps authentication notice on demo key. Fallback available.');
          this.showFallback();
        };

        const script = document.createElement('script');
        script.type = 'text/javascript';
        script.async = true;
        script.defer = true;
        script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(GOOGLE_MAPS_DEMO_KEY)}&libraries=marker&callback=${callbackName}&loading=async`;

        script.onerror = (err) => {
          this.isLoading = false;
          this.hasError = true;
          delete window[callbackName];
          reject(new Error('Google Maps script network load error.'));
        };

        setTimeout(() => {
          if (!this.isLoaded && !window.google?.maps) {
            this.isLoading = false;
            reject(new Error('Google Maps script load timeout.'));
          }
        }, 8000);

        document.head.appendChild(script);
      });

      return this.loadPromise;
    }

    /**
     * Opens the Location Viewer modal centered on the given coordinates.
     */
    async openLocationViewer(lat, lng, title = 'Shared Location', liveSessionId = null) {
      if (!this.modalEl) this.init();

      this.currentLat = Number(lat);
      this.currentLng = Number(lng);
      this.activeLiveSessionId = liveSessionId;

      const isLive = !!liveSessionId;
      if (this.badgeEl) {
        this.badgeEl.textContent = isLive ? '🔴 موقع حي مباشر' : '📍 موقع جغرافي مُشارك';
      }
      if (this.subtitleEl) {
        this.subtitleEl.textContent = title || 'موقع مُشارك';
      }

      const coordsStr = `خط العرض: ${this.currentLat.toFixed(5)}، خط الطول: ${this.currentLng.toFixed(5)}`;
      if (this.coordsLabelEl) {
        this.coordsLabelEl.textContent = coordsStr;
      }
      if (this.fallbackCoordsEl) {
        this.fallbackCoordsEl.textContent = coordsStr;
      }

      const externalUrl = `https://www.google.com/maps?q=${this.currentLat},${this.currentLng}`;
      if (this.externalLinkEl) {
        this.externalLinkEl.href = externalUrl;
      }

      // Show modal
      if (this.modalEl) {
        this.modalEl.style.display = 'flex';
      }

      // Render Google Map
      try {
        await this.loadGoogleMapsScript();
        this.renderMap(this.currentLat, this.currentLng, title);
      } catch (err) {
        console.warn('[FaceStone Map] Showing resilient direct link fallback:', err.message);
        this.showFallback();
      }
    }

    renderMap(lat, lng, title) {
      if (!this.mapCanvasEl || !window.google || !window.google.maps) {
        this.showFallback();
        return;
      }

      if (this.fallbackEl) this.fallbackEl.style.display = 'none';
      if (this.mapCanvasEl) this.mapCanvasEl.style.display = 'block';

      const centerPos = { lat, lng };

      if (!this.map) {
        const mapOptions = {
          center: centerPos,
          zoom: 16,
          mapTypeId: google.maps.MapTypeId.ROADMAP,
          streetViewControl: false,
          fullscreenControl: false,
          mapTypeControl: false,
          zoomControl: true
        };

        this.map = new google.maps.Map(this.mapCanvasEl, mapOptions);

        if (google.maps.marker && google.maps.marker.AdvancedMarkerElement) {
          this.marker = new google.maps.marker.AdvancedMarkerElement({
            position: centerPos,
            map: this.map,
            title: title || 'User Location'
          });
        } else {
          this.marker = new google.maps.Marker({
            position: centerPos,
            map: this.map,
            title: title || 'User Location',
            animation: google.maps.Animation.DROP
          });
        }
      } else {
        google.maps.event.trigger(this.map, 'resize');
        this.map.setCenter(centerPos);
        if (this.marker) {
          if (typeof this.marker.setPosition === 'function') {
            this.marker.setPosition(centerPos);
          } else {
            this.marker.position = centerPos;
          }
          if (typeof this.marker.setTitle === 'function') {
            this.marker.setTitle(title || 'User Location');
          } else {
            this.marker.title = title || 'User Location';
          }
        } else {
          if (google.maps.marker && google.maps.marker.AdvancedMarkerElement) {
            this.marker = new google.maps.marker.AdvancedMarkerElement({
              position: centerPos,
              map: this.map,
              title: title || 'User Location'
            });
          } else {
            this.marker = new google.maps.Marker({
              position: centerPos,
              map: this.map,
              title: title || 'User Location'
            });
          }
        }
      }
    }

    /**
     * Updates marker and center in real-time when tracking an active live location.
     */
    updateLiveMarker(liveSessionId, lat, lng) {
      if (!this.activeLiveSessionId || this.activeLiveSessionId !== liveSessionId) {
        return;
      }

      this.currentLat = Number(lat);
      this.currentLng = Number(lng);

      const coordsStr = `خط العرض: ${this.currentLat.toFixed(5)}، خط الطول: ${this.currentLng.toFixed(5)}`;
      if (this.coordsLabelEl) this.coordsLabelEl.textContent = coordsStr;
      if (this.fallbackCoordsEl) this.fallbackCoordsEl.textContent = coordsStr;

      const externalUrl = `https://www.google.com/maps?q=${this.currentLat},${this.currentLng}`;
      if (this.externalLinkEl) this.externalLinkEl.href = externalUrl;

      if (this.map && this.marker && window.google?.maps) {
        const newPos = { lat: this.currentLat, lng: this.currentLng };
        if (typeof this.marker.setPosition === 'function') {
          this.marker.setPosition(newPos);
        } else {
          this.marker.position = newPos;
        }
        this.map.panTo(newPos);
      }
    }

    showFallback() {
      if (this.mapCanvasEl) this.mapCanvasEl.style.display = 'none';
      if (this.fallbackEl) this.fallbackEl.style.display = 'flex';
    }

    closeModal() {
      if (this.modalEl) {
        this.modalEl.style.display = 'none';
      }
      this.activeLiveSessionId = null;
    }
  }

  window.FaceStoneMap = new FaceStoneLocationMapManager();

})(window, document);
