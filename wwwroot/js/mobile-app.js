/* ==========================================================================
   FACESTONE MOBILE APPLICATION CONTROLLER (PWA)
   Service Worker Registration, Bottom Navigation, Touch Gestures, Camera & Install Prompt
   ========================================================================== */

(function () {
  'use strict';

  class FaceStoneMobileApp {
    static init() {
      this.deferredInstallPrompt = null;
      this.activeView = 'departments';

      this.registerServiceWorker();
      this.bindDomElements();
      this.bindEvents();
      this.initNetworkStatus();
      this.initInstallPrompt();
      this.initCameraCapture();

      // Sync initial state
      this.syncActiveViewFromLocation();
      this.syncUserBadge();
    }

    static bindDomElements() {
      // Top App Bar
      this.appBar = document.getElementById('fs-mobile-appbar');
      this.appBarTitle = document.getElementById('fs-appbar-title');
      this.appBarSubtitle = document.getElementById('fs-appbar-subtitle');
      this.appBarAvatarBtn = document.getElementById('fs-appbar-avatar-btn');
      this.appBarConnPill = document.getElementById('fs-appbar-conn-pill');
      this.appBarConnText = document.getElementById('fs-appbar-conn-text');

      // Bottom Nav
      this.bottomNav = document.getElementById('fs-mobile-bottom-nav');
      this.navDepts = document.getElementById('fs-nav-depts');
      this.navChat = document.getElementById('fs-nav-chat');
      this.navTrans = document.getElementById('fs-nav-trans');
      this.navStorage = document.getElementById('fs-nav-storage');
      this.navFinance = document.getElementById('fs-nav-finance');
      this.navMgmt = document.getElementById('fs-nav-mgmt');
      this.chatBadge = document.getElementById('fs-chat-badge');

      // Profile Sheet
      this.profileBackdrop = document.getElementById('fs-profile-sheet-backdrop');
      this.sheetAvatar = document.getElementById('fs-sheet-avatar');
      this.sheetUserName = document.getElementById('fs-sheet-user-name');
      this.sheetUserRole = document.getElementById('fs-sheet-user-role');
      this.sheetInstallBtn = document.getElementById('fs-sheet-install-btn');
      this.sheetLogoutBtn = document.getElementById('fs-sheet-logout-btn');

      // Offline & Install Banners
      this.offlineBanner = document.getElementById('fs-offline-banner');
      this.installBanner = document.getElementById('fs-pwa-install-banner');
      this.bannerInstallBtn = document.getElementById('fs-banner-install-btn');
      this.bannerDismissBtn = document.getElementById('fs-banner-dismiss-btn');

      // Camera input
      this.cameraInput = document.getElementById('fs-mobile-camera-input');
      this.cameraBtn = document.getElementById('fs-mobile-camera-btn');
    }

    static bindEvents() {
      // Bottom Nav item clicks
      this.navDepts?.addEventListener('click', (e) => {
        e.preventDefault();
        this.setActiveNav('departments');
        if (window.ChatApp) window.ChatApp.backToDepartments();
      });

      this.navChat?.addEventListener('click', (e) => {
        e.preventDefault();
        this.setActiveNav('chat');
        if (window.ChatApp) window.ChatApp.enterFinanceChat();
      });

      this.navTrans?.addEventListener('click', (e) => {
        e.preventDefault();
        this.setActiveNav('transportation');
        if (window.ChatApp) window.ChatApp.enterDepartment('transportation');
      });

      this.navStorage?.addEventListener('click', (e) => {
        e.preventDefault();
        this.setActiveNav('storage');
        if (window.ChatApp) window.ChatApp.enterDepartment('storage');
      });

      this.navFinance?.addEventListener('click', (e) => {
        e.preventDefault();
        this.setActiveNav('finance');
        if (window.ChatApp) window.ChatApp.enterDepartment('finance');
      });

      this.navMgmt?.addEventListener('click', (e) => {
        e.preventDefault();
        this.setActiveNav('management');
        if (window.ChatApp) window.ChatApp.enterDepartment('management');
      });

      // App Bar Avatar -> Open Profile Sheet
      this.appBarAvatarBtn?.addEventListener('click', () => {
        this.openProfileSheet();
      });

      // Profile Sheet Backdrop Click -> Close
      this.profileBackdrop?.addEventListener('click', (e) => {
        if (e.target === this.profileBackdrop) {
          this.closeProfileSheet();
        }
      });

      // Profile Sheet Logout
      this.sheetLogoutBtn?.addEventListener('click', () => {
        this.closeProfileSheet();
        if (window.ChatApp) window.ChatApp.logout();
      });

      // Profile Sheet Install Action
      this.sheetInstallBtn?.addEventListener('click', () => {
        this.closeProfileSheet();
        this.triggerInstall();
      });

      // Listen for view changes from ChatApp
      document.addEventListener('chat:view:changed', (e) => {
        if (e.detail && e.detail.view) {
          this.setActiveNav(e.detail.view);
          this.syncUserBadge(e.detail.user);
        }
      });

      // Listen for SignalR connection status changes
      window.addEventListener('chat:connection:status', (e) => {
        this.updateConnectionStatus(e.detail?.status || 'disconnected');
      });
      window.addEventListener('realtime:status', (e) => {
        this.updateConnectionStatus(e.detail?.state || 'disconnected');
      });

      // Listen for unread chat updates
      window.addEventListener('chat:unread:updated', (e) => {
        const count = e.detail?.count || 0;
        if (this.chatBadge) {
          this.chatBadge.textContent = count > 99 ? '99+' : count;
          this.chatBadge.classList.toggle('visible', count > 0);
        }
      });

      // Window route popstate
      window.addEventListener('popstate', () => {
        this.syncActiveViewFromLocation();
      });
    }

    static registerServiceWorker() {
      if ('serviceWorker' in navigator) {
        window.addEventListener('load', () => {
          navigator.serviceWorker.register('/sw.js')
            .then((reg) => {
              console.log('[FaceStone PWA] Service Worker registered with scope:', reg.scope);
              reg.onupdatefound = () => {
                const installing = reg.installing;
                if (installing) {
                  installing.onstatechange = () => {
                    if (installing.state === 'installed' && navigator.serviceWorker.controller) {
                      console.log('[FaceStone PWA] New update available.');
                    }
                  };
                }
              };
            })
            .catch((err) => {
              console.warn('[FaceStone PWA] Service Worker registration failed:', err);
            });
        });
      }
    }

    static initNetworkStatus() {
      const updateOnlineStatus = () => {
        const isOnline = navigator.onLine;
        if (this.offlineBanner) {
          this.offlineBanner.classList.toggle('visible', !isOnline);
        }
        if (!isOnline && this.appBarConnPill) {
          this.updateConnectionStatus('offline');
        }
      };

      window.addEventListener('online', updateOnlineStatus);
      window.addEventListener('offline', updateOnlineStatus);
      updateOnlineStatus();
    }

    static initInstallPrompt() {
      window.addEventListener('beforeinstallprompt', (e) => {
        e.preventDefault();
        this.deferredInstallPrompt = e;
        // Show install banner if user hasn't dismissed it in this session
        if (!sessionStorage.getItem('fs_pwa_banner_dismissed')) {
          this.installBanner?.classList.add('visible');
        }
      });

      this.bannerInstallBtn?.addEventListener('click', () => {
        this.triggerInstall();
      });

      this.bannerDismissBtn?.addEventListener('click', () => {
        this.installBanner?.classList.remove('visible');
        sessionStorage.setItem('fs_pwa_banner_dismissed', 'true');
      });

      window.addEventListener('appinstalled', () => {
        this.deferredInstallPrompt = null;
        this.installBanner?.classList.remove('visible');
        console.log('[FaceStone PWA] App successfully installed.');
      });
    }

    static async triggerInstall() {
      if (this.deferredInstallPrompt) {
        this.deferredInstallPrompt.prompt();
        const choice = await this.deferredInstallPrompt.userChoice;
        if (choice.outcome === 'accepted') {
          console.log('[FaceStone PWA] User accepted installation.');
        }
        this.deferredInstallPrompt = null;
        this.installBanner?.classList.remove('visible');
      } else {
        // Guide for iOS Safari or browsers where prompt cannot be triggered programmatically
        const isIos = /iphone|ipad|ipod/.test(navigator.userAgent.toLowerCase());
        if (isIos) {
          alert('لتثبيت تطبيق فيس ستون على الآيفون:\n١. اضغط على زر المشاركة (Share) أسفل المتصفح.\n٢. اختر "إضافة إلى الصفحة الرئيسية" (Add to Home Screen).');
        } else {
          alert('لتثبيت تطبيق فيس ستون:\nانقر على قائمة المتصفح (⋮) ثم اختر "تثبيت التطبيق" أو "إضافة إلى الشاشة الرئيسية".');
        }
      }
    }

    static initCameraCapture() {
      this.cameraBtn?.addEventListener('click', (e) => {
        e.stopPropagation();
        this.cameraInput?.click();
      });

      this.cameraInput?.addEventListener('change', () => {
        if (this.cameraInput.files && this.cameraInput.files.length > 0) {
          const file = this.cameraInput.files[0];
          if (window.ChatApp) {
            window.ChatApp.selectedFile = file;
            if (window.ChatApp.fileNamePreview) {
              window.ChatApp.fileNamePreview.textContent = '📷 ' + file.name;
            }
            window.ChatApp.filePreviewStrip?.classList.add('active');
          }
        }
      });
    }

    static syncActiveViewFromLocation() {
      const path = window.location.pathname.toLowerCase();
      let view = 'departments';

      if (path.includes('/finance/chat')) {
        view = 'chat';
      } else if (path.includes('/transportation')) {
        view = 'transportation';
      } else if (path.includes('/storage')) {
        view = 'storage';
      } else if (path.includes('/management')) {
        view = 'management';
      } else if (path.includes('/finance')) {
        view = 'finance';
      }

      this.setActiveNav(view);
    }

    static setActiveNav(view) {
      this.activeView = view;
      const tabs = [this.navDepts, this.navChat, this.navTrans, this.navStorage, this.navFinance, this.navMgmt];
      tabs.forEach(tab => tab?.classList.remove('active'));

      let subtitle = 'لوحة التحكم';

      switch (view) {
        case 'chat':
          this.navChat?.classList.add('active');
          subtitle = 'المحادثات الفورية';
          break;
        case 'transportation':
          this.navTrans?.classList.add('active');
          subtitle = 'قسم النقل واللوجستيات';
          break;
        case 'storage':
          this.navStorage?.classList.add('active');
          subtitle = 'قسم المستودعات والتخزين';
          break;
        case 'finance':
          this.navFinance?.classList.add('active');
          subtitle = 'قسم المالية';
          break;
        case 'management':
          this.navMgmt?.classList.add('active');
          subtitle = 'إدارة المصنع';
          break;
        case 'departments':
        default:
          this.navDepts?.classList.add('active');
          subtitle = 'لوحة التحكم والأقسام';
          break;
      }

      if (this.appBarSubtitle) {
        this.appBarSubtitle.textContent = subtitle;
      }
      document.body.classList.toggle('chat-view-active', view === 'chat');
    }

    static syncUserBadge(userParam) {
      const user = userParam || (window.ChatApi ? window.ChatApi.getCurrentUser() : null);
      if (!user) return;

      const initials = (user.fullName || user.username || 'FS')
        .split(' ')
        .map(w => w[0])
        .slice(0, 2)
        .join('')
        .toUpperCase();

      if (this.appBarAvatarBtn) this.appBarAvatarBtn.textContent = initials;
      if (this.sheetAvatar) this.sheetAvatar.textContent = initials;
      if (this.sheetUserName) this.sheetUserName.textContent = user.fullName || user.username;
      if (this.sheetUserRole) this.sheetUserRole.textContent = user.role || 'عضو فيس ستون';

      // Update Department Tabs Visibility based on Role Permissions
      const role = (user.role || '').toUpperCase();
      const isCeo = role === 'CEO';
      const isIt = role === 'IT';
      const isAccountant = role.includes('ACCOUNT');
      const isTransManager = role.includes('TRANSPORT');
      const isStorageManager = role.includes('STORAGE');
      const isFactoryManager = role.includes('FACTORY');

      const hasFinance = (isCeo || isIt || isAccountant);
      const hasTrans = (isCeo || isIt || isTransManager);
      const hasStorage = (isCeo || isIt || isStorageManager);
      const hasMgmt = (isCeo || isIt || isFactoryManager);

      if (this.navFinance) this.navFinance.style.display = hasFinance ? 'flex' : 'none';
      if (this.navTrans) this.navTrans.style.display = hasTrans ? 'flex' : 'none';
      if (this.navStorage) this.navStorage.style.display = hasStorage ? 'flex' : 'none';
      if (this.navMgmt) this.navMgmt.style.display = hasMgmt ? 'flex' : 'none';
    }

    static updateConnectionStatus(status) {
      if (!this.appBarConnPill) return;

      const dot = this.appBarConnPill.querySelector('.fs-appbar-conn-dot');
      const text = this.appBarConnText;

      if (status === 'connected') {
        this.appBarConnPill.style.background = 'rgba(16, 185, 129, 0.12)';
        this.appBarConnPill.style.borderColor = 'rgba(16, 185, 129, 0.3)';
        this.appBarConnPill.style.color = '#10B981';
        if (dot) {
          dot.style.background = '#10B981';
          dot.style.boxShadow = '0 0 6px #10B981';
        }
        if (text) text.textContent = 'متصل';
      } else if (status === 'connecting' || status === 'reconnecting') {
        this.appBarConnPill.style.background = 'rgba(245, 158, 11, 0.12)';
        this.appBarConnPill.style.borderColor = 'rgba(245, 158, 11, 0.3)';
        this.appBarConnPill.style.color = '#F59E0B';
        if (dot) {
          dot.style.background = '#F59E0B';
          dot.style.boxShadow = '0 0 6px #F59E0B';
        }
        if (text) text.textContent = 'جاري الاتصال...';
      } else {
        this.appBarConnPill.style.background = 'rgba(239, 68, 68, 0.12)';
        this.appBarConnPill.style.borderColor = 'rgba(239, 68, 68, 0.3)';
        this.appBarConnPill.style.color = '#EF4444';
        if (dot) {
          dot.style.background = '#EF4444';
          dot.style.boxShadow = '0 0 6px #EF4444';
        }
        if (text) text.textContent = 'غير متصل';
      }
    }

    static openProfileSheet() {
      this.syncUserBadge();
      this.profileBackdrop?.classList.add('open');
    }

    static closeProfileSheet() {
      this.profileBackdrop?.classList.remove('open');
    }
  }

  // Initialize on DOM ready
  document.addEventListener('DOMContentLoaded', () => {
    FaceStoneMobileApp.init();
    window.FaceStoneMobileApp = FaceStoneMobileApp;
  });

})();
