(() => {
  const root = document.documentElement;
  const themeKey = "privacyguard.siteTheme";
  const langKey = "privacyguard.language";
  const themeBtn = document.getElementById("themeBtn");
  const langs = window.PG_SITE_LANGS || [];
  const tables = window.PG_SITE_I18N || {};
  const flags = window.PG_SITE_FLAGS || {};
  const en = tables["en-US"] || {};
  const extraI18n = {
    "meta.title.download": "Buy PrivacyGuard for Windows",
    "meta.desc.download": "One-time purchase. Full control over Windows privacy. Fully reversible. $19.99 for Windows 10 and 11.",
    "download.kicker": "Windows 10 and 11",
    "download.title": "Take back control of Windows privacy",
    "download.lede": "One-time purchase. Full control over Windows privacy. Fully reversible.",
    "download.official": "Official purchase",
    "download.cardTitle": "PrivacyGuard",
    "download.priceOnce": "one-time",
    "download.terms": "One-time payment · Lifetime license · No subscription",
    "download.get": "Buy now — $19.99",
    "download.badgeSecure": "Secure checkout",
    "download.badgeInstant": "Instant download",
    "download.badgeSupport": "14-day support",
    "download.includedTitle": "What's included",
    "download.incScore": "Privacy score dashboard",
    "download.incProfiles": "Recommended and custom profiles",
    "download.incHistory": "Full history and restore points",
    "download.incTray": "System tray controls",
    "download.incExport": "Export and import on this PC",
    "download.guarantee": "30-day money-back through Gumroad if it is not the right fit.",
    "download.source": "View source on GitHub",
    "download.trustLocal": "100% local — nothing leaves your PC",
    "download.trustRevert": "All changes are reversible",
    "download.trustAccount": "No Microsoft account required",
    "download.trustOss": "Open source on GitHub",
    "download.warnTitle": "Buy only from this website",
    "download.warnBody": "Checkout is on Gumroad from this page. After payment you get the x64 installer by email. Windows SmartScreen may warn until the file is signed — choose More info, then Run anyway. Do not install copies from random mirrors.",
    "download.reqAccountBody": "History and restore points stay on this PC under {path}.",
    "cta.sub": "Local, reversible, and honest about SKU limits. One-time purchase, $19.99. No Google Play listing, no account required."
  };
  Object.assign(en, extraI18n);
  for (const table of Object.values(tables)) {
    Object.assign(table, extraI18n);
  }
  const PATH_HTML = "<code>%LocalAppData%\\PrivacyGuard\\</code>";
  const PATH_TEXT = "%LocalAppData%\\PrivacyGuard\\";

  const known = (code) => langs.some((l) => l.code === code);

  const normalize = (raw) => {
    if (!raw) return null;
    if (known(raw)) return raw;
    const lower = String(raw).toLowerCase().replace("_", "-");
    const map = {
      en: "en-US",
      "en-us": "en-US",
      pt: "pt-BR",
      "pt-br": "pt-BR",
      "pt-pt": "pt-BR",
      es: "es-ES",
      "es-es": "es-ES",
      "es-mx": "es-ES",
      fr: "fr-FR",
      "fr-fr": "fr-FR",
      de: "de-DE",
      "de-de": "de-DE",
      it: "it-IT",
      "it-it": "it-IT",
      zh: "zh-CN",
      "zh-cn": "zh-CN",
      "zh-hans": "zh-CN",
      ja: "ja-JP",
      "ja-jp": "ja-JP",
      ru: "ru-RU",
      "ru-ru": "ru-RU",
      ar: "ar",
    };
    return map[lower] || map[lower.split("-")[0]] || null;
  };

  const detectLang = () => {
    try {
      const saved = normalize(localStorage.getItem(langKey));
      if (saved) return saved;
    } catch (_) {}
    const navLangs = navigator.languages && navigator.languages.length
      ? navigator.languages
      : [navigator.language || "en-US"];
    for (const item of navLangs) {
      const code = normalize(item);
      if (code) return code;
    }
    return "en-US";
  };

  const t = (key) => {
    const table = tables[currentLang] || {};
    return table[key] || en[key] || key;
  };

  const fill = (value, html) => {
    if (!value) return value;
    return value.split("{path}").join(html ? PATH_HTML : PATH_TEXT);
  };

  let currentLang = detectLang();
  let theme = "dark";
  try {
    const savedTheme = localStorage.getItem(themeKey);
    if (savedTheme === "light" || savedTheme === "dark") theme = savedTheme;
    else if (window.matchMedia("(prefers-color-scheme: light)").matches) theme = "light";
  } catch (_) {}

  const applyTheme = () => {
    if (theme === "light") root.dataset.theme = "light";
    else delete root.dataset.theme;
    root.style.colorScheme = theme;
    const colorMeta = document.querySelector('meta[name="theme-color"]');
    if (colorMeta) colorMeta.setAttribute("content", theme === "light" ? "#ececec" : "#0b0d12");
    if (themeBtn) {
      const label = t(theme === "light" ? "theme.dark" : "theme.light");
      themeBtn.textContent = label;
      themeBtn.setAttribute("aria-label", label);
    }
  };

  const applyLanguage = (code, persist) => {
    currentLang = known(code) ? code : "en-US";
    const meta = langs.find((l) => l.code === currentLang) || langs[0];
    root.lang = meta?.htmlLang || "en";
    root.dir = currentLang === "ar" ? "rtl" : "ltr";
    if (persist) {
      try { localStorage.setItem(langKey, currentLang); } catch (_) {}
    }

    document.querySelectorAll("[data-i18n]").forEach((el) => {
      const key = el.getAttribute("data-i18n");
      const html = el.hasAttribute("data-i18n-html");
      const value = fill(t(key), html);
      if (html) el.innerHTML = value;
      else el.textContent = value;
    });

    const page = document.body?.dataset.page || "home";
    document.title = t(`meta.title.${page}`);
    const desc = document.querySelector('meta[name="description"]');
    if (desc) desc.setAttribute("content", t(`meta.desc.${page}`));

    applyTheme();
    syncLangButton();
  };

  const flagMarkup = (code) => `<span class="flag" aria-hidden="true">${flags[code] || ""}</span>`;

  const langBtn = document.createElement("button");
  langBtn.type = "button";
  langBtn.className = "btn ghost lang-btn";
  langBtn.id = "langBtn";
  langBtn.setAttribute("aria-haspopup", "listbox");
  langBtn.setAttribute("aria-expanded", "false");
  langBtn.setAttribute("aria-controls", "langMenu");

  const langMenu = document.createElement("div");
  langMenu.className = "lang-menu";
  langMenu.id = "langMenu";
  langMenu.setAttribute("role", "listbox");
  langMenu.hidden = true;

  const switcher = document.createElement("div");
  switcher.className = "lang-switch";
  switcher.append(langBtn, langMenu);
  themeBtn?.parentNode?.insertBefore(switcher, themeBtn);

  const syncLangButton = () => {
    const meta = langs.find((l) => l.code === currentLang);
    langBtn.innerHTML = `${flagMarkup(currentLang)}<span class="lang-current">${meta ? meta.native : "English"}</span><span class="lang-caret" aria-hidden="true"></span>`;
    langBtn.setAttribute("aria-label", t("lang.choose"));
    langBtn.setAttribute("title", t("lang.choose"));
    langMenu.querySelectorAll("[role='option']").forEach((opt) => {
      const on = opt.dataset.code === currentLang;
      opt.classList.toggle("on", on);
      opt.setAttribute("aria-selected", on ? "true" : "false");
    });
  };

  langs.forEach((lang) => {
    const opt = document.createElement("button");
    opt.type = "button";
    opt.className = "lang-option";
    opt.setAttribute("role", "option");
    opt.dataset.code = lang.code;
    opt.innerHTML = `${flagMarkup(lang.code)}<span>${lang.native}</span>`;
    opt.addEventListener("click", () => {
      applyLanguage(lang.code, true);
      closeMenu();
    });
    langMenu.appendChild(opt);
  });

  const closeMenu = () => {
    langMenu.hidden = true;
    langBtn.setAttribute("aria-expanded", "false");
  };

  const openMenu = () => {
    langMenu.hidden = false;
    langBtn.setAttribute("aria-expanded", "true");
  };

  langBtn.addEventListener("click", (e) => {
    e.stopPropagation();
    if (langMenu.hidden) openMenu();
    else closeMenu();
  });

  document.addEventListener("click", () => closeMenu());
  langMenu.addEventListener("click", (e) => e.stopPropagation());
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") closeMenu();
  });

  themeBtn?.addEventListener("click", () => {
    theme = theme === "light" ? "dark" : "light";
    applyTheme();
    try { localStorage.setItem(themeKey, theme); } catch (_) {}
  });

  applyLanguage(currentLang, false);
})();
