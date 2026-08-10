-- ISE Elite TimescaleDB Schema
-- Purpose: Track all trades, tick data, and daily P&L across 100+ accounts
-- Created: August 10, 2026

-- Create extension
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- ============================================================================
-- ACCOUNTS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS ise_accounts (
    account_id SERIAL PRIMARY KEY,
    account_name VARCHAR(100) NOT NULL UNIQUE,
    start_date TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    initial_equity DECIMAL(12,2) NOT NULL,
    current_equity DECIMAL(12,2) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'active', -- active, paused, closed
    max_contracts_mnq INTEGER NOT NULL DEFAULT 4,
    max_contracts_mgc INTEGER NOT NULL DEFAULT 3,
    daily_loss_limit DECIMAL(12,2) NOT NULL DEFAULT -1000.00,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ============================================================================
-- TRADES TABLE (Time-series optimized)
-- ============================================================================
CREATE TABLE IF NOT EXISTS ise_trades (
    trade_id BIGSERIAL NOT NULL,
    account_id INTEGER NOT NULL REFERENCES ise_accounts(account_id),
    entry_time TIMESTAMPTZ NOT NULL,
    exit_time TIMESTAMPTZ NOT NULL,
    instrument VARCHAR(10) NOT NULL, -- MNQ, MGC
    entry_price DECIMAL(10,2) NOT NULL,
    exit_price DECIMAL(10,2) NOT NULL,
    contracts INTEGER NOT NULL,
    pnl DECIMAL(12,2) NOT NULL,
    mode VARCHAR(20) NOT NULL, -- Trending, Ranging
    exit_reason VARCHAR(50) NOT NULL, -- Profit Target, Stop Loss, Timeout
    entry_signal_strength DECIMAL(5,2),
    slippage DECIMAL(10,2), -- actual fill vs expected
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (trade_id, entry_time)
);

-- Convert to hypertable for time-series optimization
SELECT create_hypertable('ise_trades', 'entry_time', if_not_exists => TRUE);
CREATE INDEX IF NOT EXISTS idx_trades_account_time ON ise_trades(account_id, entry_time DESC);
CREATE INDEX IF NOT EXISTS idx_trades_instrument ON ise_trades(instrument, entry_time DESC);

-- ============================================================================
-- TICK DATA TABLE (Raw market data per bar)
-- ============================================================================
CREATE TABLE IF NOT EXISTS ise_tick_data (
    tick_id BIGSERIAL NOT NULL,
    account_id INTEGER NOT NULL REFERENCES ise_accounts(account_id),
    instrument VARCHAR(10) NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL,
    open_price DECIMAL(10,4) NOT NULL,
    high_price DECIMAL(10,4) NOT NULL,
    low_price DECIMAL(10,4) NOT NULL,
    close_price DECIMAL(10,4) NOT NULL,
    volume BIGINT NOT NULL,
    atr DECIMAL(10,4),
    adx DECIMAL(5,2),
    order_flow_bias DECIMAL(5,2),
    regime VARCHAR(20), -- Trending, Ranging
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (tick_id, timestamp)
);

-- Convert to hypertable
SELECT create_hypertable('ise_tick_data', 'timestamp', if_not_exists => TRUE);
CREATE INDEX IF NOT EXISTS idx_tick_account_time ON ise_tick_data(account_id, timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_tick_instrument ON ise_tick_data(instrument, timestamp DESC);

-- ============================================================================
-- DAILY P&L TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS ise_daily_pnl (
    daily_pnl_id SERIAL PRIMARY KEY,
    account_id INTEGER NOT NULL REFERENCES ise_accounts(account_id),
    trading_date DATE NOT NULL,
    opening_equity DECIMAL(12,2) NOT NULL,
    closing_equity DECIMAL(12,2) NOT NULL,
    daily_pnl DECIMAL(12,2) NOT NULL,
    trade_count INTEGER NOT NULL,
    winning_trades INTEGER NOT NULL,
    losing_trades INTEGER NOT NULL,
    win_rate DECIMAL(5,2),
    daily_score DECIMAL(10,2),
    daily_target DECIMAL(12,2),
    target_hit BOOLEAN,
    max_intraday_dd DECIMAL(12,2),
    profit_target_exits INTEGER,
    stop_loss_exits INTEGER,
    avg_slippage DECIMAL(10,4),
    error_count INTEGER DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(account_id, trading_date)
);

-- ============================================================================
-- POSITION STATE TABLE (Current open positions)
-- ============================================================================
CREATE TABLE IF NOT EXISTS ise_position_state (
    position_id SERIAL PRIMARY KEY,
    account_id INTEGER NOT NULL REFERENCES ise_accounts(account_id),
    instrument VARCHAR(10) NOT NULL,
    contracts INTEGER NOT NULL,
    entry_price DECIMAL(10,2) NOT NULL,
    entry_time TIMESTAMPTZ NOT NULL,
    current_pnl DECIMAL(12,2),
    mode VARCHAR(20),
    target_price DECIMAL(10,2),
    stop_price DECIMAL(10,2),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(account_id, instrument) -- Only one open position per instrument per account
);

-- ============================================================================
-- SAFETY EVENTS TABLE
-- ============================================================================
CREATE TABLE IF NOT EXISTS ise_safety_events (
    event_id BIGSERIAL PRIMARY KEY,
    account_id INTEGER NOT NULL REFERENCES ise_accounts(account_id),
    event_type VARCHAR(50) NOT NULL, -- DrawdownLimit, ConnectionError, SlippageSpike, NewsEvent, etc
    severity VARCHAR(20) NOT NULL, -- warning, critical
    message TEXT,
    action_taken VARCHAR(100), -- blocks entry, closes position, etc
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_safety_events_account_time ON ise_safety_events(account_id, timestamp DESC);

-- ============================================================================
-- PERFORMANCE SUMMARY TABLE (Aggregated for dashboards)
-- ============================================================================
CREATE TABLE IF NOT EXISTS ise_performance_summary (
    summary_id SERIAL PRIMARY KEY,
    account_id INTEGER NOT NULL REFERENCES ise_accounts(account_id),
    period_start DATE NOT NULL,
    period_end DATE NOT NULL,
    total_trades INTEGER,
    winning_trades INTEGER,
    losing_trades INTEGER,
    win_rate DECIMAL(5,2),
    gross_pnl DECIMAL(12,2),
    net_pnl DECIMAL(12,2),
    max_drawdown DECIMAL(12,2),
    sharpe_ratio DECIMAL(5,2),
    profit_factor DECIMAL(5,2),
    days_hit_target INTEGER,
    total_days INTEGER,
    avg_daily_pnl DECIMAL(12,2),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(account_id, period_start, period_end)
);

-- ============================================================================
-- GRANTS (if using non-admin user)
-- ============================================================================
-- GRANT ALL ON ALL TABLES IN SCHEMA public TO ise_user;
-- GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO ise_user;

-- ============================================================================
-- CREATE VIEWS FOR QUICK QUERIES
-- ============================================================================

-- Daily performance across all accounts
CREATE OR REPLACE VIEW vw_daily_performance AS
SELECT 
    account_id,
    trading_date,
    daily_pnl,
    trade_count,
    win_rate,
    target_hit,
    max_intraday_dd
FROM ise_daily_pnl
ORDER BY trading_date DESC, account_id;

-- Recent trades with slippage
CREATE OR REPLACE VIEW vw_recent_trades AS
SELECT 
    account_id,
    entry_time,
    exit_time,
    instrument,
    entry_price,
    exit_price,
    contracts,
    pnl,
    mode,
    exit_reason,
    slippage
FROM ise_trades
WHERE entry_time > NOW() - INTERVAL '7 days'
ORDER BY entry_time DESC;

-- Active positions
CREATE OR REPLACE VIEW vw_active_positions AS
SELECT 
    account_id,
    instrument,
    contracts,
    entry_price,
    entry_time,
    current_pnl,
    target_price,
    stop_price
FROM ise_position_state
WHERE contracts > 0;
