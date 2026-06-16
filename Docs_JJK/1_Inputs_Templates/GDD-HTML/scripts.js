// Tailwind configuration for Play CDN - dynamically loads before/after CDN script execution
window.tailwind = window.tailwind || {};
window.tailwind.config = {
    theme: {
        extend: {
            colors: {
                shibuya: {
                    bg: '#050608',
                    concrete: '#0d0f14',
                    slate: '#161922',
                    dark: '#1c202a',
                    crimson: '#ff003c',
                    jade: '#00ff7f',
                    amber: '#f5a623',
                    purple: '#bc00dd',
                    metal: '#2e3648',
                }
            },
            fontFamily: {
                bebas: ['Bebas Neue', 'sans-serif'],
                mono: ['JetBrains Mono', 'monospace'],
                outfit: ['Outfit', 'sans-serif'],
                tc: ['Noto Sans TC', 'sans-serif'],
            },
            boxShadow: {
                'neon-crimson': '0 0 15px rgba(255, 0, 60, 0.25)',
                'neon-jade': '0 0 15px rgba(0, 255, 127, 0.25)',
                'neon-amber': '0 0 15px rgba(245, 166, 35, 0.25)',
                'figma-crimson': '4px 4px 0px 0px #ff003c',
                'figma-jade': '4px 4px 0px 0px #00ff7f',
                'figma-amber': '4px 4px 0px 0px #f5a623',
            }
        }
    }
};

/* Core Interactive UI Elements */
window.addEventListener('DOMContentLoaded', () => {
    // Scroll active link highlight tracking
    const sections = document.querySelectorAll('section');
    if (sections.length > 0) {
        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                const id = entry.target.getAttribute('id');
                if (entry.intersectionRatio > 0.15) {
                    document.querySelectorAll('.nav-link').forEach(a => {
                        a.classList.remove('active', 'text-white', 'bg-shibuya-slate');
                        a.classList.add('text-gray-400');
                    });
                    const activeLink = document.querySelector(`.nav-link[href$="#${id}"]`);
                    if (activeLink) {
                        activeLink.classList.add('active', 'text-white', 'bg-shibuya-slate');
                        activeLink.classList.remove('text-gray-400');
                    }
                }
            });
        }, { threshold: [0.15, 0.5], rootMargin: "0px 0px -40% 0px" });

        sections.forEach(section => {
            observer.observe(section);
        });
    }

    // Dynamic Filter Search (filtering sections available on current page)
    const searchInput = document.getElementById('gddSearch');
    if (searchInput) {
        searchInput.addEventListener('input', function(e) {
            const val = e.target.value.toLowerCase();
            document.querySelectorAll('section').forEach(sec => {
                const text = sec.textContent.toLowerCase();
                sec.style.display = text.includes(val) ? 'block' : 'none';
            });
        });
    }

    // Scroll Top Floating Button
    const scrollTopBtn = document.getElementById('scrollTopBtn');
    if (scrollTopBtn) {
        window.addEventListener('scroll', () => {
            if (window.scrollY > 300) {
                scrollTopBtn.classList.remove('opacity-0', 'pointer-events-none');
            } else {
                scrollTopBtn.classList.add('opacity-0', 'pointer-events-none');
            }
        });
        scrollTopBtn.addEventListener('click', () => {
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });
    }

    // Load persistent local storage states
    if (document.getElementById('debtList')) {
        loadDebtState();
    }
    if (document.getElementById('timelineList')) {
        loadRoadmapState();
    }
    if (document.getElementById('potUpgrade')) {
        recalcStats();
    }
});

/* Image Lightbox modal controllers */
const lightboxModal = document.getElementById('lightboxModal');
const lightboxImg = lightboxModal ? lightboxModal.querySelector('.lightbox-img') : null;

window.openLightbox = function(src) {
    if (lightboxModal && lightboxImg) {
        lightboxImg.src = src;
        lightboxModal.classList.remove('hidden');
        lightboxModal.classList.add('flex');
    }
};

if (lightboxModal) {
    lightboxModal.addEventListener('click', () => {
        lightboxModal.classList.add('hidden');
        lightboxModal.classList.remove('flex');
    });
}

/* Tab switcher */
window.switchTab = function(evt, tabId) {
    const container = evt.currentTarget.closest('.tabs-container');
    if (!container) return;
    container.querySelectorAll('.tab-btn').forEach(btn => {
        btn.classList.remove('active', 'border-shibuya-jade', 'text-shibuya-jade', 'border-shibuya-crimson', 'text-shibuya-crimson');
        btn.classList.add('border-shibuya-metal/30', 'text-gray-400');
    });
    container.querySelectorAll('.tab-pane').forEach(pane => pane.classList.remove('active'));
    
    evt.currentTarget.classList.add('active');
    if(tabId === 'kept-tab') {
        evt.currentTarget.classList.add('border-shibuya-jade', 'text-shibuya-jade');
        evt.currentTarget.classList.remove('border-shibuya-metal/30', 'text-gray-400');
    } else {
        evt.currentTarget.classList.add('border-shibuya-crimson', 'text-shibuya-crimson');
        evt.currentTarget.classList.remove('border-shibuya-metal/30', 'text-gray-400');
    }
    const targetPane = document.getElementById(tabId);
    if (targetPane) targetPane.classList.add('active');
};

/* Timeline Accordions */
window.toggleTimeline = function(id) {
    const item = document.getElementById(id);
    if (!item) return;
    const isActive = item.classList.contains('active');
    
    document.querySelectorAll('.timeline-item').forEach(el => {
        el.classList.remove('active');
    });
    
    if (!isActive) {
        item.classList.add('active');
    }
};

/* Character Simulator Systems */
const charData = {
    shin: {
        name: "SHIN",
        role: "Main DPS",
        roleClass: "bg-shibuya-crimson/20 border-shibuya-crimson text-shibuya-crimson",
        tagline: '"Spirit Fire Aura, fast blade combos"',
        actions: {
            ba: {
                name: "Flame Arc Chain",
                windup: 8, active: 10, recovery: 8,
                log: "State FSM: Basic 3-hit string. (Hit 1: 5 DMG | Hit 2: 7 DMG | Hit 3: 12 DMG). Spawned hitbox (OverlapBoxNonAlloc). VFX: Red Arc slash."
            },
            skill: {
                name: "Aura Burst",
                windup: 12, active: 18, recovery: 12,
                log: "State FSM: Aura Burst triggered. Directional steer active. VFX: Explosion. Staggered minor trash targets."
            },
            ult: {
                name: "Cursed Inferno",
                windup: 25, active: 30, recovery: 20,
                log: "SPECIAL GRADED ULTIMATE: Cursed Inferno. Full screenshake triggers. Camera slow-mo activated. Hit-stop: 12 frames."
            }
        }
    },
    blind: {
        name: "BLIND",
        role: "Sub-DPS",
        roleClass: "bg-shibuya-purple/20 border-shibuya-purple text-shibuya-purple",
        tagline: '"Aura projectile triggers, quick positioning support"',
        actions: {
            ba: {
                name: "Spectral Needle",
                windup: 6, active: 8, recovery: 6,
                log: "State FSM: Ranged arrow trigger. Fired tracking projectle. VFX: Purple tracer."
            },
            skill: {
                name: "Cursed Echo",
                windup: 10, active: 15, recovery: 10,
                log: "State FSM: Echo circle spawn. Targets inside take +10% Cursed DMG. Buff inherits on team swap."
            },
            ult: {
                name: "Blind Judgement",
                windup: 20, active: 40, recovery: 15,
                log: "SPECIAL GRADED ULTIMATE: Blind Judgement. Storm projectile sweeps target coordinates. Play SFX: High crescendo."
            }
        }
    },
    sage: {
        name: "SAGE",
        role: "Support",
        roleClass: "bg-shibuya-jade/20 border-shibuya-jade text-shibuya-jade",
        tagline: '"Aura Barrier shielding, healing zones"',
        actions: {
            ba: {
                name: "Stave Stagger",
                windup: 12, active: 8, recovery: 14,
                log: "State FSM: Staff strike. Low recovery. Staggered front shield targets."
            },
            skill: {
                name: "Cursed Shield",
                windup: 8, active: 5, recovery: 10,
                log: "State FSM: Barrier cast. Absorbs damage equal to 25% max HP. SFX: Glass shimmer."
            },
            ult: {
                name: "Sanctuary Ward",
                windup: 15, active: 60, recovery: 15,
                log: "SPECIAL GRADED ULTIMATE: Sanctuary Ward. Healing zone active. Restores 3% HP tick to roster members inside."
            }
        }
    }
};

let currentActiveSimChar = 'shin';
let isExecutingSimAction = false;

window.selectSimChar = function(key) {
    if (isExecutingSimAction) return;
    currentActiveSimChar = key;
    document.querySelectorAll('.char-select-btn').forEach(btn => {
        btn.className = "char-select-btn px-3 py-1 bg-shibuya-slate border border-shibuya-metal text-gray-400 font-bebas text-sm font-bold tracking-wider";
    });
    
    const evt = window.event;
    if (evt && evt.currentTarget) {
        evt.currentTarget.className = "char-select-btn active px-3 py-1 bg-shibuya-crimson text-black font-bebas text-sm font-bold tracking-wider shadow-neon-crimson";
    }

    const data = charData[key];
    if (!data) return;
    const badge = document.getElementById('charBadge');
    if (badge) {
        badge.textContent = data.role;
        badge.className = `text-[9px] font-mono px-2 py-0.5 tracking-widest uppercase border ${data.roleClass}`;
    }

    const nameEl = document.getElementById('charName');
    if (nameEl) nameEl.textContent = data.name;

    const tagEl = document.getElementById('charTag');
    if (tagEl) tagEl.textContent = data.tagline;

    const buttons = document.querySelectorAll('.trigger-btn');
    if (buttons.length >= 3) {
        buttons[0].querySelector('span').textContent = data.actions.ba.name;
        buttons[1].querySelector('span').textContent = data.actions.skill.name;
        buttons[2].querySelector('span').textContent = data.actions.ult.name;
    }

    addConsoleLine(`SWAPPED ACTIVE TERMINAL TO ${data.name}. READY.`);
};

window.triggerAction = function(actionKey) {
    if (isExecutingSimAction) return;
    isExecutingSimAction = true;
    
    const evt = window.event;
    const btn = evt ? evt.currentTarget : null;
    if (btn) {
        btn.classList.add('border-shibuya-crimson', 'bg-shibuya-crimson/10');
    }

    const char = charData[currentActiveSimChar];
    const action = char.actions[actionKey];

    const totalFrames = action.windup + action.active + action.recovery;
    const wPct = (action.windup / totalFrames) * 100;
    const aPct = (action.active / totalFrames) * 100;
    const rPct = (action.recovery / totalFrames) * 100;

    const wPhase = document.getElementById('wPhase');
    const aPhase = document.getElementById('aPhase');
    const rPhase = document.getElementById('rPhase');

    if (wPhase) wPhase.style.width = `${wPct}%`;
    if (aPhase) aPhase.style.width = `${aPct}%`;
    if (rPhase) rPhase.style.width = `${rPct}%`;

    const indicator = document.getElementById('frameIndicator');
    if (indicator) {
        indicator.style.display = 'block';
        indicator.style.left = '0%';
    }

    addConsoleLine(`FSM STATE: ${action.name.toUpperCase()} CAST. WINDUP:${action.windup}F | ACTIVE:${action.active}F | RECOVER:${action.recovery}F`);

    let start = null;
    const duration = totalFrames * 16.67; // 60fps frame count translation in ms
    
    function animateIndicator(timestamp) {
        if (!start) start = timestamp;
        const progress = timestamp - start;
        const pct = Math.min((progress / duration) * 100, 100);
        if (indicator) indicator.style.left = `${pct}%`;

        if (progress < duration) {
            requestAnimationFrame(animateIndicator);
        } else {
            if (indicator) indicator.style.display = 'none';
            if (btn) btn.classList.remove('border-shibuya-crimson', 'bg-shibuya-crimson/10');
            isExecutingSimAction = false;
            addConsoleLine(`SUCCESS: ${action.log}`);
        }
    }
    requestAnimationFrame(animateIndicator);
};

window.addConsoleLine = function(text) {
    const consoleBox = document.getElementById('consoleOutput');
    if (!consoleBox) return;
    const newLine = document.createElement('div');
    newLine.textContent = `> ${text}`;
    consoleBox.appendChild(newLine);
    consoleBox.scrollTop = consoleBox.scrollHeight;
};

/* Stats upgrade calculations */
window.recalcStats = function() {
    let baseAtk = 10;
    let baseHp = 100;
    let baseCrit = 5;
    let baseCdr = 0;

    const potUpgradeEl = document.getElementById('potUpgrade');
    if (!potUpgradeEl) return;
    const potLvl = parseInt(potUpgradeEl.value);
    
    if (potLvl >= 1) baseAtk += 10;
    if (potLvl >= 2) { baseAtk += 10; baseCdr += 5; }
    if (potLvl >= 3) { baseAtk += 20; baseCdr += 5; baseCrit += 10; }
    if (potLvl >= 4) { baseAtk += 30; baseCdr += 10; baseCrit += 10; }

    const baseAtkBeforeCharms = baseAtk;
    const baseCritBeforeCharms = baseCrit;
    const baseCdrBeforeCharms = baseCdr;

    const upAtk = document.getElementById('upAtk');
    const upCdr = document.getElementById('upCdr');
    const upCrit = document.getElementById('upCrit');

    const hasAtkCharm = upAtk ? upAtk.checked : false;
    const hasCdrCharm = upCdr ? upCdr.checked : false;
    const hasCritCharm = upCrit ? upCrit.checked : false;

    if (hasAtkCharm) baseAtk = Math.round(baseAtk * 1.15);
    if (hasCdrCharm) baseCdr += 10;
    if (hasCritCharm) baseCrit += 15;

    const valAtk = document.getElementById('valAtk');
    if (valAtk) valAtk.textContent = baseAtk;

    const valOrig = document.querySelector('.calc-stat-val-orig');
    if (valOrig) valOrig.textContent = potLvl > 0 ? (potLvl >= 3 ? 30 : 20) : 10;

    const valCrit = document.getElementById('valCrit');
    if (valCrit) valCrit.textContent = `${baseCrit}%`;

    const valCdr = document.getElementById('valCdr');
    if (valCdr) valCdr.textContent = `${baseCdr}%`;

    const diffCrit = document.getElementById('diffCrit');
    const diffCdr = document.getElementById('diffCdr');

    if (diffCrit) diffCrit.textContent = baseCrit > baseCritBeforeCharms ? `(+${baseCrit - baseCritBeforeCharms}%)` : '';
    if (diffCdr) diffCdr.textContent = baseCdr > baseCdrBeforeCharms ? `(+${baseCdr - baseCdrBeforeCharms}%)` : '';
    
    const cooldownLabel = document.getElementById('cooldownLabel');
    if (cooldownLabel) cooldownLabel.textContent = `CDR: ${baseCdr}%`;
};

/* Persistent checklists (Debt list) */
window.saveDebtState = function() {
    const states = {};
    document.querySelectorAll('#debtList input[type="checkbox"]').forEach(box => {
        states[box.id] = box.checked;
    });
    localStorage.setItem('jjk_debt_states', JSON.stringify(states));
};

window.loadDebtState = function() {
    const data = localStorage.getItem('jjk_debt_states');
    if (data) {
        const states = JSON.parse(data);
        Object.keys(states).forEach(id => {
            const box = document.getElementById(id);
            if (box) box.checked = states[id];
        });
    }
};

/* Persistent roadmap checklist */
window.saveRoadmap = function() {
    const states = {};
    document.querySelectorAll('#timelineList input[type="checkbox"]').forEach(box => {
        states[box.id] = box.checked;
    });
    localStorage.setItem('jjk_roadmap_states', JSON.stringify(states));
    updateTimelineBadgeStates();
};

window.loadRoadmapState = function() {
    const data = localStorage.getItem('jjk_roadmap_states');
    if (data) {
        const states = JSON.parse(data);
        Object.keys(states).forEach(id => {
            const box = document.getElementById(id);
            if (box) box.checked = states[id];
        });
    }
    updateTimelineBadgeStates();
};

window.updateTimelineBadgeStates = function() {
    document.querySelectorAll('.timeline-item').forEach(item => {
        const checkboxes = item.querySelectorAll('input[type="checkbox"]');
        const total = checkboxes.length;
        let checkedCount = 0;
        checkboxes.forEach(cb => { if (cb.checked) checkedCount++; });

        const badge = item.querySelector('.timeline-badge');
        if (badge) {
            if (total > 0 && checkedCount === total) {
                badge.classList.remove('bg-shibuya-bg');
                badge.classList.add('bg-shibuya-jade');
            } else {
                badge.classList.add('bg-shibuya-bg');
                badge.classList.remove('bg-shibuya-jade');
            }
        }
    });
};
